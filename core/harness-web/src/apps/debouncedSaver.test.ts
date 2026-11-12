import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { createDebouncedSaver } from "./debouncedSaver";

describe("createDebouncedSaver", () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it("saves only the last value after the quiet period", () => {
    const save = vi.fn();
    const saver = createDebouncedSaver<string[]>(save, 800);
    saver.schedule(["a"]);
    vi.advanceTimersByTime(500);
    saver.schedule(["a", "b"]);
    vi.advanceTimersByTime(799);
    expect(save).not.toHaveBeenCalled();
    vi.advanceTimersByTime(1);
    expect(save).toHaveBeenCalledTimes(1);
    expect(save).toHaveBeenCalledWith(["a", "b"]);
  });

  it("does not save what the server already has", () => {
    const save = vi.fn();
    const saver = createDebouncedSaver<string[]>(save, 800);
    saver.markSaved(["a"]);
    saver.schedule(["a"]);
    vi.advanceTimersByTime(1000);
    expect(save).not.toHaveBeenCalled();
  });

  it("does not save the same value twice in a row", () => {
    const save = vi.fn();
    const saver = createDebouncedSaver<string[]>(save, 800);
    saver.schedule(["a"]);
    vi.advanceTimersByTime(800);
    saver.schedule(["a"]);
    vi.advanceTimersByTime(800);
    expect(save).toHaveBeenCalledTimes(1);
  });

  it("flush saves at once, and nothing happens later", () => {
    const save = vi.fn();
    const saver = createDebouncedSaver<string[]>(save, 800);
    saver.schedule(["a"]);
    saver.flush();
    expect(save).toHaveBeenCalledWith(["a"]);
    vi.advanceTimersByTime(2000);
    expect(save).toHaveBeenCalledTimes(1);
  });

  it("cancel drops the pending value", () => {
    const save = vi.fn();
    const saver = createDebouncedSaver<string[]>(save, 800);
    saver.schedule(["a"]);
    saver.cancel();
    vi.advanceTimersByTime(2000);
    saver.flush();
    expect(save).not.toHaveBeenCalled();
  });
});
