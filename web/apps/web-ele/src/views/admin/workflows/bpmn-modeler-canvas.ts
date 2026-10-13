const elementTypeNames: { [type: string]: string } = {
  'bpmn:EndEvent': '结束',
  'bpmn:ExclusiveGateway': '条件分支',
  'bpmn:InclusiveGateway': '包容分支',
  'bpmn:ParallelGateway': '并行审批',
  'bpmn:ServiceTask': '自动任务',
  'bpmn:SequenceFlow': '流转线',
  'bpmn:StartEvent': '发起',
  'bpmn:UserTask': '审批节点',
};

export function describeSelectedElement(element: any) {
  const type = element?.businessObject?.$type as string | undefined;
  const elementTypeText = type
    ? elementTypeNames[type] || '流程元素'
    : '未选择';
  const businessObject = element?.businessObject;
  const name = businessObject?.name || element?.id;
  return name ? `${name} · ${elementTypeText}` : elementTypeText;
}

export function changeZoom(modeler: any, delta: number) {
  if (!modeler) return;
  const canvas = modeler.get('canvas');
  canvas.zoom(canvas.zoom() + delta);
}

export function fitViewport(modeler: any) {
  if (!modeler) return;
  modeler.get('canvas').zoom('fit-viewport');
}

export function beginCreateShape(
  modeler: any,
  event: DragEvent | MouseEvent,
  type: string,
) {
  const elementFactory = modeler.get('elementFactory');
  const create = modeler.get('create');
  create.start(event, elementFactory.createShape({ type }));
}

export function placeShapeOnCanvas(modeler: any, type: string) {
  const canvas = modeler.get('canvas');
  const modeling = modeler.get('modeling');
  const selection = modeler.get('selection');
  const elementFactory = modeler.get('elementFactory');
  const rootElement = canvas.getRootElement();
  const viewbox = canvas.viewbox();
  const childCount = rootElement.children?.length ?? 0;
  const stagger = (childCount % 5) * 24;
  const shape = elementFactory.createShape({ type });
  const createdShape = modeling.createShape(
    shape,
    {
      x: viewbox.x + viewbox.width / 2 + stagger,
      y: viewbox.y + viewbox.height / 2 + stagger,
    },
    rootElement,
  );

  selection.select(createdShape);
  canvas.scrollToElement(createdShape);
}
