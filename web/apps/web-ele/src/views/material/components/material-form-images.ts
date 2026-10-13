import type { UploadRequestOptions, UploadUserFile } from 'element-plus';

import { ref } from 'vue';

import { ElMessage } from 'element-plus';

import { loadAssetImageObjectUrl, uploadAssetImageApi } from '#/api/asset';
import { createAsyncSessionTracker } from '#/utils/async-session-tracker';
import { createObjectUrlLifecycle } from '#/utils/object-url-lifecycle';

export type AuthenticatedUploadFile = { rawUrl?: string } & UploadUserFile;

export type MaterialFormState = {
  brand: string;
  custodianId?: number;
  departmentId?: number;
  locationName: string;
  model: string;
  name: string;
  projectId?: number;
  quantity: number;
  receivedDate?: string;
  remark: string;
  vendorName: string;
};

export function createMaterialFormImages() {
  const attachmentMaxMb = ref(5);
  const pendingUploads = createAsyncSessionTracker();
  const uploading = ref(false);
  const imageFileList = ref<AuthenticatedUploadFile[]>([]);
  let imageLoadGeneration = 0;
  const uploadedImageUrls = createObjectUrlLifecycle();

  function revokeImageObjectUrls() {
    imageFileList.value.forEach((file) => {
      if (file.url?.startsWith('blob:')) URL.revokeObjectURL(file.url);
      const responseUrl = (file.response as { url?: string } | undefined)?.url;
      if (responseUrl?.startsWith('blob:') && responseUrl !== file.url)
        URL.revokeObjectURL(responseUrl);
    });
  }

  function nextGeneration() {
    imageLoadGeneration += 1;
    return imageLoadGeneration;
  }

  function closeSession() {
    pendingUploads.close();
    uploading.value = false;
    uploadedImageUrls.close();
    revokeImageObjectUrls();
    imageFileList.value = [];
  }

  function openSession() {
    pendingUploads.start();
    uploadedImageUrls.open();
  }

  function dispose() {
    uploadedImageUrls.close();
    revokeImageObjectUrls();
  }

  function onImageRemove(file: AuthenticatedUploadFile) {
    if (file.url?.startsWith('blob:')) URL.revokeObjectURL(file.url);
    const responseUrl = (file.response as { url?: string } | undefined)?.url;
    if (responseUrl?.startsWith('blob:') && responseUrl !== file.url)
      URL.revokeObjectURL(responseUrl);
  }

  function clearFiles() {
    imageFileList.value = [];
  }

  async function loadExistingImages(
    readImages: () => null | string[] | undefined,
    generation: number,
    isOpen: () => boolean,
  ) {
    const results = await Promise.allSettled(
      (readImages() ?? []).map(async (rawUrl, index) => ({
        name: rawUrl.split('/').pop() ?? rawUrl,
        rawUrl,
        status: 'success' as const,
        uid: -(index + 1),
        url: await loadAssetImageObjectUrl(rawUrl),
      })),
    );
    const sourceImages = readImages() ?? [];
    const files: AuthenticatedUploadFile[] = results.map((result, index) => {
      if (result.status === 'fulfilled') return result.value;
      const rawUrl = sourceImages[index] ?? '';
      return {
        name: rawUrl.split('/').pop() ?? rawUrl,
        rawUrl,
        status: 'success',
        uid: -(index + 1),
      };
    });
    if (generation !== imageLoadGeneration || !isOpen()) {
      files.forEach((file) => {
        if (file.url) URL.revokeObjectURL(file.url);
      });
      return;
    }
    imageFileList.value = files;
    const failedCount = results.filter(
      (result) => result.status === 'rejected',
    ).length;
    if (failedCount > 0) {
      ElMessage.warning(`有 ${failedCount} 张原照片加载失败，保存前请确认`);
    }
  }

  function beforeImageUpload(file: File) {
    const allowed = ['image/gif', 'image/jpeg', 'image/png', 'image/webp'];
    if (!allowed.includes(file.type)) {
      ElMessage.warning('仅支持 jpg/png/gif/webp 格式图片');
      return false;
    }
    if (file.size > attachmentMaxMb.value * 1024 * 1024) {
      ElMessage.warning(`单张图片大小不能超过 ${attachmentMaxMb.value}MB`);
      return false;
    }
    return true;
  }

  function customImageUpload(options: UploadRequestOptions) {
    const sessionToken = pendingUploads.token();
    const uploadGeneration = uploadedImageUrls.token();
    const request = uploadAssetImageApi(options.file).then(async (uploaded) => {
      const url = await loadAssetImageObjectUrl(uploaded.url);
      if (!uploadedImageUrls.adopt(url, uploadGeneration)) {
        throw new DOMException('上传会话已关闭', 'AbortError');
      }
      return {
        ...uploaded,
        rawUrl: uploaded.url,
        url,
      };
    });
    pendingUploads.track(request, sessionToken);
    uploading.value = true;
    void request.then(
      () => {
        if (pendingUploads.isCurrent(sessionToken))
          uploading.value = pendingUploads.hasPending(sessionToken);
      },
      () => {
        if (pendingUploads.isCurrent(sessionToken))
          uploading.value = pendingUploads.hasPending(sessionToken);
      },
    );
    return request;
  }

  function onImageExceed() {
    ElMessage.warning('最多上传 5 张照片');
  }

  return {
    attachmentMaxMb,
    beforeImageUpload,
    clearFiles,
    closeSession,
    customImageUpload,
    dispose,
    imageFileList,
    loadExistingImages,
    nextGeneration,
    onImageExceed,
    onImageRemove,
    openSession,
    pendingUploads,
    uploading,
  };
}
