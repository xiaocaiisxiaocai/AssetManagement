export const PASSWORD_DIALOG_WIDTH = 'min(500px, calc(100vw - 24px))';

export function closePasswordDialogUnlessSubmitting(
  submitting: boolean,
  done: () => void,
) {
  if (submitting) return false;
  done();
  return true;
}
