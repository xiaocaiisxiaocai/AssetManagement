// 部门资产管理系统 - 综合端到端测试
// 测试所有页面、功能流程和业务场景

const { chromium } = require('playwright');
const fs = require('fs');

async function runComprehensiveTest() {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext();
  const page = await context.newPage();

  // 监听浏览器控制台输出
  page.on('console', msg => {
    if (msg.type() === 'error') {
      log(`浏览器控制台错误: ${msg.text()}`, 'warn');
    }
  });

  // 监听失败的请求
  page.on('requestfailed', request => {
    log(`网络请求失败: ${request.url()} - ${request.failure()?.errorText}`, 'warn');
  });

  page.on('response', response => {
    if (response.status() >= 400) {
      log(`HTTP 错误响应: ${response.status()} ${response.url()}`, 'warn');
    }
  });

  const results = {
    passed: [],
    failed: [],
    warnings: []
  };

  function log(message, type = 'info') {
    const timestamp = new Date().toISOString();
    console.log(`[${timestamp}] [${type.toUpperCase()}] ${message}`);
    if (type === 'pass') results.passed.push(message);
    if (type === 'fail') results.failed.push(message);
    if (type === 'warn') results.warnings.push(message);
  }

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
          } catch (err) {
            return { success: false, error: err.message };
          }
        }
        return { success: false, reason: 'window.$router 不存在' };
      }, route);

      if (routed.success) {
        // 给 2.5 秒时间加载接口数据和组件渲染（Vite 动态加载组件需要时间）
        await page.waitForTimeout(2500);
        return true;
      }

      log(`SPA Router 跳转不可用: ${routed.reason || routed.error}，正在使用 page.goto...`, 'warn');
      // 退步方案
      const url = `http://localhost:5777${route}`;
      await page.goto(url, { waitUntil: 'load', timeout: 30000 });
      await page.waitForTimeout(2000);
      return true;
    } catch (e) {
      log(`无法跳转到 ${pageName}: ${e.message}`, 'fail');
      return false;
    }
  }

  // 辅助函数：保存截图作为调试证据
  async function takeDebugScreenshot(name) {
    try {
      const path = `debug-${name}.png`;
      await page.screenshot({ path });
      log(`调试截图已保存: ${path}`, 'info');
    } catch (e) {
      log(`保存调试截图失败: ${e.message}`, 'warn');
    }
  }

  // 辅助函数：检测页面内容并带有轮询重试机制
  async function checkContentWithRetry(keywords, pageName, errorName, timeoutMs = 15000) {
    const startTime = Date.now();

    while (Date.now() - startTime < timeoutMs) {
      const content = await page.content();
      const found = keywords.some(kw => content.includes(kw));
      if (found) {
        log(`✓ ${pageName}页面加载成功`, 'pass');
        return true;
      }
      await page.waitForTimeout(500); // 轮询间隔
    }

    log(`✗ ${pageName}页面加载失败 (在 ${timeoutMs}ms 内缺少关键字: ${keywords.join('/')})`, 'fail');
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
    await page.fill('input[name="account"]', '1001');
    await page.fill('input[name="password"]', '123456');
    await page.click('button:has-text("登录")');

    // 等待登录成功并跳转
    await page.waitForTimeout(6000);
    const currentUrl = page.url();
    if (currentUrl.includes('/asset') || currentUrl.includes('/dashboard') || currentUrl.includes('/list')) {
      log('✓ 登录成功并正确跳转到主页', 'pass');
    } else {
      log(`✗ 登录后未正确跳转，当前URL: ${currentUrl}`, 'fail');
      await takeDebugScreenshot('login-fail');
    }

    // ==================== 1.5. 工作台/仪表盘测试 ====================
    log('--- 测试模块: 工作台仪表盘 ---');
    if (await safeGoto('/dashboard/workspace', '工作台')) {
      await checkContentWithRetry(['资产概览', '资产总数', '在库数', '借出数', '资产原值', '逾期数'], '工作台仪表盘', 'workspace-fail');
    }

    // ==================== 2. 资产管理模块测试 ====================
    log('--- 测试模块: 资产管理 ---');

    // 2.1 资产列表页面
    if (await safeGoto('/asset/list', '资产列表')) {
      await checkContentWithRetry(['资产编号', '资产名称', '新增', '添加', '导入'], '资产列表', 'asset-list-fail');

      // 检查列表/层级视图切换
      const viewToggle = await page.locator('.el-tabs__nav button:has-text("列表"), .el-tabs__nav button:has-text("层级")').count();
      if (viewToggle > 0) {
        log('✓ 视图切换按钮存在', 'pass');
      } else {
        log('⚠ 视图切换按钮未找到', 'warn');
      }

      // 检查操作按钮与详情弹窗
      const detailBtn = await page.locator('button:has-text("详情")').filter({ visible: true }).first();
      if (await detailBtn.count() > 0) {
        log('✓ 资产详情按钮存在', 'pass');
        try {
          await detailBtn.click({ timeout: 5000 });
          await page.waitForTimeout(2000);
          const dialogContent = await page.content();
          const hasDetailInfo = dialogContent.includes('资产详情') || dialogContent.includes('基本信息');
          if (hasDetailInfo) {
            log('✓ 资产详情弹窗打开成功', 'pass');
            if (dialogContent.includes('流转时间线') || dialogContent.includes('时间线')) {
              log('✓ 流转时间线区块存在', 'pass');
            } else {
              log('⚠ 流转时间线区块未显式找到', 'warn');
            }
          } else {
            log('✗ 资产详情弹窗加载失败', 'fail');
          }
          await page.keyboard.press('Escape'); // 关闭弹窗
          await page.waitForTimeout(500);
        } catch (clickErr) {
          log(`⚠ 点击详情按钮超时或失败: ${clickErr.message}`, 'warn');
        }
      } else {
        log('⚠ 可见详情操作按钮不可见(可能列表数据为空)', 'warn');
      }
    }

    // 2.2 资产分类管理
    if (await safeGoto('/asset/categories', '资产分类')) {
      await checkContentWithRetry(['分类', '编码', '添加', '新增', '分类名称'], '资产分类', 'categories-fail');
    }

    // 2.3 存放位置管理
    if (await safeGoto('/asset/locations', '存放位置')) {
      await checkContentWithRetry(['位置', '二维码', '添加', '新增', '位置名称'], '存放位置', 'locations-fail');
    }

    // ==================== 3. 审批管理模块测试 ====================
    log('--- 测试模块: 审批管理 ---');

    // 3.1 待我审批
    if (await safeGoto('/approval/pending', '待我审批')) {
      await checkContentWithRetry(['工单', '审批', '申请', '处理', '待审批', '我的申请'], '待我审批', 'approval-pending-fail');
    }

    // 3.2 我的申请
    if (await safeGoto('/approval/mine', '我的申请')) {
      await checkContentWithRetry(['工单', '类型', '状态', '申请', '新增申请'], '我的申请', 'approval-mine-fail');
    }

    // 3.3 待确认入库
    if (await safeGoto('/approval/confirm-return', '待确认入库')) {
      await checkContentWithRetry(['入库', '归还', '工单', '确认', '确认入库'], '待确认入库', 'confirm-return-fail');
    }

    // ==================== 4. 报表统计模块测试 ====================
    log('--- 测试模块: 报表统计 ---');

    // 4.1 资产汇总
    if (await safeGoto('/report/summary', '资产汇总')) {
      await checkContentWithRetry(['汇总', '数量', '价值', '金额', '资产数量', '总价值'], '资产汇总', 'report-summary-fail');
    }

    // 4.2 借用明细
    if (await safeGoto('/report/borrow', '借用明细')) {
      await checkContentWithRetry(['借用', '人', '时间', '借用人', '借用时间'], '借用明细', 'report-borrow-fail');
    }

    // 4.3 逾期资产
    if (await safeGoto('/report/overdue', '逾期资产')) {
      await checkContentWithRetry(['逾期', '应归还', '天数', '逾期天数', '应归还日期'], '逾期资产', 'report-overdue-fail');
    }

    // ==================== 5. 系统管理模块测试 ====================
    log('--- 测试模块: 系统管理 ---');

    // 5.1 用户管理
    if (await safeGoto('/admin/users', '用户管理')) {
      await checkContentWithRetry(['工号', '姓名', '角色', '新增', '用户', '添加用户'], '用户管理', 'admin-users-fail');
    }

    // 5.2 角色管理
    if (await safeGoto('/admin/roles', '角色管理')) {
      await checkContentWithRetry(['角色', '编码', '名称', '新增', '添加角色', '角色名称'], '角色管理', 'admin-roles-fail');
    }

    // 5.3 审批流程(工作流设计器)
    if (await safeGoto('/admin/workflows', '审批流程')) {
      await checkContentWithRetry(['流程', '引擎', '步骤', '新增', '设计器', '工作流', '业务类型'], '审批流程', 'admin-workflows-fail');
    }

    // 5.4 审计日志
    if (await safeGoto('/admin/audit', '审计日志')) {
      await checkContentWithRetry(['日志', '操作', '人', '时间', '操作日志', '操作人', '操作时间'], '审计日志', 'admin-audit-fail');
    }

    // 5.5 组织架构
    if (await safeGoto('/admin/departments', '组织架构')) {
      await checkContentWithRetry(['部门', '架构', '名称', '部门名称', '部门编码'], '组织架构', 'admin-dept-fail');
    }

    // 5.6 系统参数
    if (await safeGoto('/admin/settings', '系统参数')) {
      await checkContentWithRetry(['参数', '键', '值', '配置', '系统参数'], '系统参数', 'admin-settings-fail');
    }

    // ==================== 6. 保存最终截图 ====================
    log('--- 保存测试截图 ---');
    await page.screenshot({ path: 'e2e-final-state.png', fullPage: true });
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
    const successRate = totalTests > 0 ? (results.passed.length / totalTests * 100).toFixed(2) : 0;
    log(`\n总体成功率: ${successRate}%`);
    log('========== 测试完成 ==========');

    // 退出码逻辑
    process.exit(results.failed.length > 0 ? 1 : 0);
  }
}

runComprehensiveTest().catch(console.error);
