import type { VxeComponentSizeType } from 'vxe-pc-ui';
import type { VxeGridPropTypes } from 'vxe-table';

export type CommonOperationType = {
  add?: {
    handleClick: (params?: any) => void;
    params?: any;
    permissionCode: string;
  };
  delete?: {
    handleClick: (params?: any) => void;
    params?: any;
    permissionCode: string;
  };
  edit?: {
    handleClick: (params?: any) => void;
    params?: any;
    permissionCode: string;
  };
  export?: {
    handleClick: (params?: any) => void;
    params?: any;
    permissionCode: string;
  };
  import?: {
    handleClick: (params?: any) => void;
    params?: any;
    permissionCode: string;
  };
  view?: {
    handleClick: (params?: any) => void;
    params?: any;
    permissionCode: string;
  };
};

export interface PurestGridProps {
  height?: number;
  columns: Array<any> | VxeGridPropTypes.Columns<any>;
  customToolbarActions?: VxeGridPropTypes.ToolbarConfig;
  commonOperation?: CommonOperationType | undefined;
  request: (params: any) => Promise<any>;
  rowKey?: string;
  size?: undefined | VxeComponentSizeType;
  treeConfig?: any;
  customePager?: {
    pageIndex: number;
    pageSize: number;
    total: number;
  };
  searchOptions?: {
    formData: any;
    formItems: Array<any>;
    reset: () => void;
    submit: (params?: any) => void;
  };
}
