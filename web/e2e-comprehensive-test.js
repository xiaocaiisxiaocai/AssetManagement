// 部门资产管理系统 - 综合端到端测试
// 测试所有页面、功能流程和业务场景

/* eslint-disable no-console */

import process from 'node:process';

import { chromium } from 'playwright';

async function runComprehensiveTest() {
  const testEmployeeNo = process.env.E2E_EMPLOYEE_NO || '1001';
  const testPassword = process.env.E2E_PASSWORD;
  if (!testPassword) {
    throw new Error(
      '缺少 E2E_PASSWORD 环境变量，拒绝在测试脚本中保存默认或真实密码',
    );
  }
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext();
  const page = await context.newPage();

  const results = {
    failed: [],
    passed: [],
    warnings: [],
  };
  const failedHttpResponses = new Set();

  function log(message, type = 'info') {
    const timestamp = new Date().toISOString();
    console.log(`[${timestamp}] [${type.toUpperCase()}] ${message}`);
    if (type === 'pass') results.passed.push(message);
    if (type === 'fail') results.failed.push(message);
    if (type === 'warn') results.warnings.push(message);
  }

  // 监听浏览器控制台输出
  page.on('console', (msg) => {
    if (msg.type() === 'error') {
      log(`浏览器控制台错误: ${msg.text()}`, 'fail');
    }
  });

  // 监听失败的请求
  page.on('requestfailed', (request) => {
    log(
      `网络请求失败: ${request.url()} - ${request.failure()?.errorText}`,
      'fail',
    );
  });

  page.on('response', (response) => {
    if (
      response.status() >= 400 &&
      !failedHttpResponses.has(`${response.status()} ${response.url()}`)
    ) {
      failedHttpResponses.add(`${response.status()} ${response.url()}`);
      log(`HTTP 错误响应: ${response.status()} ${response.url()}`, 'fail');
    }
  });

  // 辅助函数：安全地前往URL（支持Vue Router和传统跳转）
  async function safeGoto(route, pageName) {
    try {
      log(`正在导航到 ${pageName} (${route})...`);

      // 尝试使用 Vue Router 进行 SPA 无刷新跳转
      const routed = await page.evaluate(async (path) => {
        if (window.$router) {
          try {
            await window.$router.push(path);
            return { success: true };
          } catch (error) {
            return { error: error.message, success: false };
          }
        }
        return { reason: 'window.$router 不存在', success: false };
      }, route);

      if (routed.success) {
        // 给 2.5 秒时间加载接口数据和组件渲染（Vite 动态加载组件需要时间）
        await page.waitForTimeout(2500);
        return true;
      }

      log(
        `SPA Router 跳转不可用: ${routed.reason || routed.error}，正在使用 page.goto...`,
        'warn',
      );
      // 退步方案
      const url = `http://localhost:5777${route}`;
      await page.goto(url, { timeout: 30_000, waitUntil: 'load' });
      await page.waitForTimeout(2000);
      return true;
    } catch (error) {
      log(`无法跳转到 ${pageName}: ${error.message}`, 'fail');
      return false;
    }
  }

  // 辅助函数：保存截图作为调试证据
  async function takeDebugScreenshot(name) {
    try {
      const path = `debug-${name}.png`;
      await page.screenshot({ path });
      log(`调试截图已保存: ${path}`, 'info');
    } catch (error) {
      log(`保存调试截图失败: ${error.message}`, 'warn');
    }
  }

  // 辅助函数：检测页面内容并带有轮询重试机制
  async function checkContentWithRetry(
    keywords,
    pageName,
    errorName,
    timeoutMs = 15_000,
  ) {
    const startTime = Date.now();

    while (Date.now() - startTime < timeoutMs) {
      const content = await page.content();
      const found = keywords.some((kw) => content.includes(kw));
      if (found) {
        log(`✓ ${pageName}页面加载成功`, 'pass');
        return true;
      }
      await page.waitForTimeout(500); // 轮询间隔
    }

    log(
      `✗ ${pageName}页面加载失败 (在 ${timeoutMs}ms 内缺少关键字: ${keywords.join('/')})`,
      'fail',
    );
    await takeDebugScreenshot(errorName);
    return false;
  }

  try {
    log('========== 开始综合测试 ==========');

    // ==================== 1. 登录测试 ====================
    log('--- 测试模块: 登录认证 ---');
    await page.goto('http://localhost:5777/');
    await page.waitForTimeout(4000);

    // 检查登录页面元素
    const loginTitle = await page.locator('text=资产管理系统').count();
    if (loginTitle > 0) {
      log('✓ 登录页面标题显示正确', 'pass');
    } else {
      log('⚠ 登录页面标题未显式找到', 'warn');
    }

    // 测试登录
    await page.fill('input[name="account"]', testEmployeeNo);
    await page.fill('input[name="password"]', testPassword);
    await page.click('button:has-text("登录")');

    // 等待登录成功并跳转（修正：实际跳转到 /home）
    await page.waitForTimeout(6000);
    const currentUrl = page.url();
    if (
      currentUrl.includes('/home') ||
      currentUrl.includes('/asset') ||
      currentUrl.includes('/dashboard')
    ) {
      log('✓ 登录成功并正确跳转到主页', 'pass');
    } else {
      log(`✗ 登录后未正确跳转，当前URL: ${currentUrl}`, 'fail');
      await takeDebugScreenshot('login-fail');
    }

    // ==================== 1.5. 工作台/仪表盘测试 ====================
    log('--- 测试模块: 工作台仪表盘 ---');
    // 修正：实际路由是 /home，增加超时到 30 秒，关键字更新
    if (await safeGoto('/home', '工作台')) {
      await checkContentWithRetry(
        ['资产总数', '在库资产', '借出资产', '逾期资产', '待办提醒'],
        '工作台仪表盘',
        'workspace-fail',
        30_000,
      );
    }

    // ==================== 2. 资产管理模块测试 ====================
    log('--- 测试模块: 资产管理 ---');

    // 2.1 资产列表页面
    if (await safeGoto('/asset/list', '资产列表')) {
      await checkContentWithRetry(
        ['资产编号', '资产名称', '新增', '添加', '导入'],
        '资产列表',
        'asset-list-fail',
      );

      // 检查分类浏览/全部资产清单切换，并进入清单以验证后续操作。
      const showAllAssets = page.getByRole('button', {
        exact: true,
        name: '查看全部资产',
      });
      const backToCategories = page.getByRole('button', {
        exact: true,
        name: '返回分类浏览',
      });
      try {
        if (await showAllAssets.count()) await showAllAssets.click();
        await backToCategories.waitFor({ state: 'visible', timeout: 10_000 });
        log('✓ 分类浏览与全部资产清单切换正常', 'pass');
      } catch {
        log('✗ 分类浏览与全部资产清单切换失败', 'fail');
      }

      // 存放位置已改为资产表单中的手工输入项，不再使用独立数据字典页面。
      const createAssetButton = page.getByRole('button', {
        exact: true,
        name: '新增资产',
      });
      if (await createAssetButton.count()) {
        try {
          await createAssetButton.click();
          const locationInput = page.getByPlaceholder(
            '请输入存放位置，如：三楼研发区 A-12',
          );
          await locationInput.waitFor({ state: 'visible', timeout: 10_000 });
          const manualLocation = 'E2E 手工填写位置 A-12';
          await locationInput.fill(manualLocation);
          if ((await locationInput.inputValue()) === manualLocation) {
            log('✓ 存放位置支持手工填写', 'pass');
          } else {
            log('✗ 存放位置手工填写值未正确保留', 'fail');
          }
          await page.keyboard.press('Escape');
          await page.waitForTimeout(500);
        } catch (error) {
          log(`✗ 存放位置手工填写检查失败: ${error.message}`, 'fail');
          await takeDebugScreenshot('asset-location-input-fail');
          await page.keyboard.press('Escape').catch(() => {});
        }
      } else {
        log('✗ 未找到新增资产按钮，无法检查存放位置手工填写', 'fail');
      }

      // 检查操作按钮与详情弹窗
      const detailBtn = await page
        .locator('button:has-text("详情")')
        .filter({ visible: true })
        .first();
      if ((await detailBtn.count()) > 0) {
        log('✓ 资产详情按钮存在', 'pass');
        try {
          await detailBtn.click({ timeout: 5000 });
          await page.waitForTimeout(2000);
          const dialogContent = await page.content();
          const hasDetailInfo =
            dialogContent.includes('资产详情') ||
            dialogContent.includes('基本信息');
          if (hasDetailInfo) {
            log('✓ 资产详情弹窗打开成功', 'pass');
            if (
              dialogContent.includes('流转记录') ||
              dialogContent.includes('流转时间线') ||
              dialogContent.includes('时间线')
            ) {
              log('✓ 流转时间线区块存在', 'pass');
            } else {
              log('⚠ 流转时间线区块未显式找到', 'warn');
            }
          } else {
            log('✗ 资产详情弹窗加载失败', 'fail');
          }
          await page.keyboard.press('Escape'); // 关闭弹窗
          await page.waitForTimeout(500);
        } catch (error) {
          log(`⚠ 点击详情按钮超时或失败: ${error.message}`, 'warn');
        }
      } else {
        try {
          await page
            .getByText('暂无数据', { exact: true })
            .waitFor({ state: 'visible', timeout: 5000 });
          log('✓ 空资产清单状态展示正确', 'pass');
        } catch {
          log('✗ 资产清单既无详情操作也无空状态', 'fail');
        }
      }
    }

    // 2.2 资产分类管理
    if (await safeGoto('/asset/categories', '资产分类')) {
      await checkContentWithRetry(
        ['分类', '编码', '添加', '新增', '分类名称'],
        '资产分类',
        'categories-fail',
      );
    }

    // ==================== 3. 审批管理模块测试 ====================
    log('--- 测试模块: 审批管理 ---');

    // 3.1 待我审批
    if (await safeGoto('/approval/pending', '待我审批')) {
      await checkContentWithRetry(
        ['工单', '审批', '申请', '处理', '待审批', '我的申请'],
        '待我审批',
        'approval-pending-fail',
      );
    }

    // 3.2 我的申请
    if (await safeGoto('/approval/mine', '我的申请')) {
      await checkContentWithRetry(
        ['工单', '类型', '状态', '申请', '新增申请'],
        '我的申请',
        'approval-mine-fail',
      );
      const transferButton = page.getByRole('button', { name: '发起转让' });
      if (await transferButton.count()) {
        await transferButton.click();
        const recipient = page.getByText('接收人', { exact: true });
        try {
          await recipient.waitFor({ state: 'visible', timeout: 10_000 });
          log('✓ 转让申请强制提供接收人选择', 'pass');
        } catch {
          log('✗ 转让申请缺少接收人选择', 'fail');
        }
        await page.keyboard.press('Escape');
      }
    }

    // 3.3 待确认入库
    if (await safeGoto('/approval/confirm-return', '待确认入库')) {
      await checkContentWithRetry(
        ['入库', '归还', '工单', '确认', '确认入库'],
        '待确认入库',
        'confirm-return-fail',
      );
    }

    // ==================== 4. 报表统计模块测试 ====================
    log('--- 测试模块: 报表统计 ---');

    // 4.1 资产汇总
    if (await safeGoto('/report/summary', '资产汇总')) {
      await checkContentWithRetry(
        ['汇总', '数量', '价值', '金额', '资产数量', '总价值'],
        '资产汇总',
        'report-summary-fail',
      );
    }

    // 4.2 借用明细
    if (await safeGoto('/report/borrow', '借用明细')) {
      await checkContentWithRetry(
        ['借用', '人', '时间', '借用人', '借用时间'],
        '借用明细',
        'report-borrow-fail',
      );
    }

    // 4.3 逾期资产
    if (await safeGoto('/report/overdue', '逾期资产')) {
      await checkContentWithRetry(
        ['逾期', '应归还', '天数', '逾期天数', '应归还日期'],
        '逾期资产',
        'report-overdue-fail',
      );
    }

    // ==================== 5. 系统管理模块测试 ====================
    log('--- 测试模块: 系统管理 ---');

    // 5.1 用户管理
    if (await safeGoto('/admin/users', '用户管理')) {
      await checkContentWithRetry(
        ['工号', '姓名', '角色', '新增', '用户', '添加用户'],
        '用户管理',
        'admin-users-fail',
      );
    }

    // 5.2 角色管理
    if (await safeGoto('/admin/roles', '角色管理')) {
      await checkContentWithRetry(
        ['角色', '编码', '名称', '新增', '添加角色', '角色名称'],
        '角色管理',
        'admin-roles-fail',
      );
    }

    // 5.3 审批流程(工作流设计器)
    if (await safeGoto('/admin/workflows', '审批流程')) {
      await checkContentWithRetry(
        ['流程', '引擎', '步骤', '新增', '设计器', '工作流', '业务类型'],
        '审批流程',
        'admin-workflows-fail',
      );
    }

    // 5.4 审计日志
    if (await safeGoto('/admin/audit', '审计日志')) {
      await checkContentWithRetry(
        ['日志', '操作', '人', '时间', '操作日志', '操作人', '操作时间'],
        '审计日志',
        'admin-audit-fail',
      );
    }

    // 5.5 组织架构
    if (await safeGoto('/admin/departments', '组织架构')) {
      await checkContentWithRetry(
        ['部门', '架构', '名称', '部门名称', '部门编码'],
        '组织架构',
        'admin-dept-fail',
      );
    }

    // 5.6 系统参数
    if (await safeGoto('/admin/settings', '系统参数')) {
      await checkContentWithRetry(
        ['参数', '键', '值', '配置', '系统参数'],
        '系统参数',
        'admin-settings-fail',
      );
    }

    // ==================== 6. 新产品新技术模块 ====================
    log('--- 测试模块: 新产品新技术 ---');
    if (await safeGoto('/material/home', '项目总览')) {
      await checkContentWithRetry(
        ['总测评数', '已结案', '进行中', '已落地'],
        '项目总览',
        'material-home-fail',
      );
    }
    if (await safeGoto('/material/projects', '测试项目')) {
      await checkContentWithRetry(
        ['项目编号', '项目名称', '负责人', '下次跟进'],
        '测试项目',
        'material-projects-fail',
      );
    }

    if (await safeGoto('/admin/backups', '数据库备份')) {
      await checkContentWithRetry(
        ['数据库备份', '备份文件', '立即备份'],
        '数据库备份',
        'admin-backups-fail',
      );
    }

    // 窄屏回归：固定像素弹窗不得超出视口。
    await page.setViewportSize({ height: 844, width: 390 });
    if (await safeGoto('/admin/users', '移动端用户管理')) {
      const createUser = page.getByRole('button', { name: /新增用户/ });
      if (await createUser.count()) {
        await createUser.click();
        const dialog = page.locator('.el-dialog:visible');
        const box = await dialog.boundingBox();
        if (box && box.x >= 0 && box.x + box.width <= 390)
          log('✓ 移动端弹窗未超出视口', 'pass');
        else log('✗ 移动端弹窗超出视口', 'fail');
        await page.keyboard.press('Escape');
      }
    }
    await page.setViewportSize({ height: 720, width: 1280 });

    // ==================== 7. 保存最终截图 ====================
    log('--- 保存测试截图 ---');
    await page.screenshot({ fullPage: true, path: 'e2e-final-state.png' });
    log('✓ 最终页面测试截图已保存: e2e-final-state.png', 'pass');
  } catch (error) {
    log(`✗ 测试过程出现严重错误: ${error.message}`, 'fail');
    console.error(error);
  } finally {
    await browser.close();

    // ==================== 测试总结 ====================
    log('');
    log('========== 测试结果汇总 ==========');
    log(`✓ 通过: ${results.passed.length} 项`);
    log(`✗ 失败: ${results.failed.length} 项`);
    log(`⚠ 警告: ${results.warnings.length} 项`);
    log('');

    if (results.failed.length > 0) {
      log('失败项目详情:');
      results.failed.forEach((item, index) => {
        log(`  ${index + 1}. ${item}`);
      });
    }

    if (results.warnings.length > 0) {
      log('警告项目详情:');
      results.warnings.forEach((item, index) => {
        log(`  ${index + 1}. ${item}`);
      });
    }

    const totalTests = results.passed.length + results.failed.length;
    const successRate =
      totalTests > 0
        ? ((results.passed.length / totalTests) * 100).toFixed(2)
        : 0;
    log(`\n总体成功率: ${successRate}%`);
    log('========== 测试完成 ==========');

    // 退出码逻辑
    process.exitCode = results.failed.length > 0 ? 1 : 0;
  }
}

runComprehensiveTest().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
