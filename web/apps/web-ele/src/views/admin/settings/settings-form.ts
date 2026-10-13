export type SettingFormState = {
  description: string;
  key: string;
  value: number | string | undefined;
};

export type SettingValueType =
  | 'asset-condition-options'
  | 'audit-retention-days'
  | 'boolean'
  | 'integer'
  | 'text'
  | 'time';
