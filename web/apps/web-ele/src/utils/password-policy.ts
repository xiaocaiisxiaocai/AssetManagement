export const PASSWORD_RULE_PATTERN = String.raw`^(?=.*[A-Za-z])(?=.*\d)[\s\S]{8,64}$`;
export const PASSWORD_RULE_MESSAGE = '密码须为 8-64 位，且同时包含字母和数字';
