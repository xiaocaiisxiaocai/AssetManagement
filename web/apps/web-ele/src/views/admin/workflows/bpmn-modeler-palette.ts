interface PaletteActivate {
  click: (event: Event, source?: any) => void;
  dragstart: (event: Event, source?: any) => void;
}

interface PaletteEntry {
  action?: PaletteActivate;
  className?: string;
  group?: string;
  title?: string;
}

interface PaletteEntries {
  [entryId: string]: PaletteEntry;
}

const allowedPaletteEntries = new Set([
  'create.end-event',
  'create.exclusive-gateway',
  'create.inclusive-gateway',
  'create.parallel-gateway',
  'create.service-task',
  'create.start-event',
  'create.task',
  'global-connect-tool',
  'hand-tool',
  'lasso-tool',
  'tool-separator',
]);

const allowedContextPadEntries = new Set([
  'append.append-task',
  'append.end-event',
  'append.gateway',
  'connect',
  'delete',
]);

function copyPaletteEntries(entries: PaletteEntries, allowed: Set<string>) {
  const nextEntries: PaletteEntries = {};
  for (const key of allowed) {
    const entry = entries[key];
    if (entry) nextEntries[key] = entry;
  }
  return nextEntries;
}

export function installWorkflowPalette(modelerInstance: any) {
  const palette = modelerInstance.get('palette');
  const contextPad = modelerInstance.get('contextPad');
  const elementFactory = modelerInstance.get('elementFactory');
  const create = modelerInstance.get('create');
  const autoPlace = modelerInstance.get('autoPlace', false);

  function createShapeEntry(
    type: string,
    group: string,
    className: string,
    title: string,
  ): PaletteEntry {
    const createShape = (event: Event) => {
      const shape = elementFactory.createShape({ type });
      create.start(event, shape);
    };

    return {
      action: {
        click: createShape,
        dragstart: createShape,
      },
      className,
      group,
      title,
    };
  }

  palette.registerProvider(500, {
    getPaletteEntries() {
      return (entries: PaletteEntries) => {
        const nextEntries = copyPaletteEntries(entries, allowedPaletteEntries);
        const exclusiveGateway = nextEntries['create.exclusive-gateway'];
        if (exclusiveGateway) {
          nextEntries['create.exclusive-gateway'] = {
            ...exclusiveGateway,
            className: 'bpmn-icon-gateway-xor',
            title: '创建条件网关',
          };
          nextEntries['create.parallel-gateway'] = createShapeEntry(
            'bpmn:ParallelGateway',
            'gateway',
            'bpmn-icon-gateway-parallel',
            '创建并行网关',
          );
        }

        const taskEntry = nextEntries['create.task'];
        if (taskEntry) {
          nextEntries['create.task'] = createShapeEntry(
            'bpmn:UserTask',
            'activity',
            'bpmn-icon-user-task',
            '创建审批节点',
          );
        }

        nextEntries['create.inclusive-gateway'] = createShapeEntry(
          'bpmn:InclusiveGateway',
          'gateway',
          'bpmn-icon-gateway-or',
          '创建包容网关',
        );
        nextEntries['create.service-task'] = createShapeEntry(
          'bpmn:ServiceTask',
          'activity',
          'bpmn-icon-service-task',
          '创建自动任务',
        );

        return nextEntries;
      };
    },
  });

  contextPad.registerProvider(500, {
    getContextPadEntries() {
      return (entries: PaletteEntries) => {
        const nextEntries = copyPaletteEntries(
          entries,
          allowedContextPadEntries,
        );
        const appendTask = nextEntries['append.append-task'];
        if (appendTask) {
          const appendUserTaskStart = (event: Event, source?: any) => {
            const shape = elementFactory.createShape({
              type: 'bpmn:UserTask',
            });
            create.start(event, shape, { source });
          };
          const appendUserTask = (event: Event, source?: any) => {
            const shape = elementFactory.createShape({
              type: 'bpmn:UserTask',
            });
            if (autoPlace) {
              autoPlace.append(source, shape);
            } else {
              appendUserTaskStart(event, source);
            }
          };
          nextEntries['append.append-task'] = {
            ...appendTask,
            action: {
              click: appendUserTask,
              dragstart: appendUserTaskStart,
            },
            className: 'bpmn-icon-user-task',
            title: '追加审批节点',
          };
        }

        const appendGateway = nextEntries['append.gateway'];
        if (appendGateway) {
          nextEntries['append.gateway'] = {
            ...appendGateway,
            title: '追加条件/并行网关',
          };
        }

        return nextEntries;
      };
    },
  });
}
