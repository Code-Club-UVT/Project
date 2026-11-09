/**
 * Saves the latest value after it has been quiet for `delayMs`, and only when it differs from what was last saved.
 * `flush` saves at once (the page is being hidden); `markSaved` tells it what the server already has.
 */
export interface DebouncedSaver<T> {
  schedule(value: T): void;
  flush(): void;
  cancel(): void;
  markSaved(value: T): void;
}

export function createDebouncedSaver<T>(
  save: (value: T) => void,
  delayMs: number,
): DebouncedSaver<T> {
  let timer: ReturnType<typeof setTimeout> | null = null;
  let pending: { value: T } | null = null;
  let lastSaved: string | null = null;

  const run = () => {
    timer = null;
    if (!pending) return;
    const { value } = pending;
    pending = null;
    const serialized = JSON.stringify(value);
    if (serialized === lastSaved) return;
    lastSaved = serialized;
    save(value);
  };

  return {
    schedule(value) {
      pending = { value };
      if (timer) clearTimeout(timer);
      timer = setTimeout(run, delayMs);
    },
    flush() {
      if (timer) clearTimeout(timer);
      run();
    },
    cancel() {
      if (timer) clearTimeout(timer);
      timer = null;
      pending = null;
    },
    markSaved(value) {
      lastSaved = JSON.stringify(value);
    },
  };
}
