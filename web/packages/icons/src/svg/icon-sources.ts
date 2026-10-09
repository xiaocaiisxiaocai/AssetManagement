const svgIconSources = import.meta.glob<string>('./icons/**', {
  eager: true,
  import: 'default',
  query: '?raw',
});

export { svgIconSources };
