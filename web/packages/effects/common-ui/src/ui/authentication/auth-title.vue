<template>
  <div class="mb-8 w-full">
    <h2
      class="text-foreground mb-3 text-3xl font-bold leading-9 tracking-tight lg:text-4xl"
    >
      <slot></slot>
    </h2>

    <p class="text-muted-foreground text-sm lg:text-base">
      <slot name="desc"></slot>
    </p>
  </div>
</template>
