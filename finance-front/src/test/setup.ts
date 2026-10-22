import '@testing-library/jest-dom/vitest';
import '@src/shared/utils/zod';
import { cleanup, configure } from '@testing-library/react';
import { toast } from 'sonner';
import { afterAll, afterEach, beforeAll } from 'vitest';

import { resetFakeDatabase } from './fake-api/fakeDatabase';
import { server } from './server';

const asyncUtilTimeoutInMilliseconds = 5000;

class ResizeObserverStub {
  observe(): void {
    return;
  }

  unobserve(): void {
    return;
  }

  disconnect(): void {
    return;
  }
}

function createMediaQueryList(query: string): MediaQueryList {
  return {
    matches: false,
    media: query,
    onchange: null,
    addListener: () => undefined,
    removeListener: () => undefined,
    addEventListener: () => undefined,
    removeEventListener: () => undefined,
    dispatchEvent: () => false,
  };
}

Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: createMediaQueryList,
});
globalThis.ResizeObserver =
  ResizeObserverStub as unknown as typeof ResizeObserver;
Element.prototype.hasPointerCapture = (): boolean => false;
Element.prototype.releasePointerCapture = (): void => undefined;
Element.prototype.setPointerCapture = (): void => undefined;
Element.prototype.scrollIntoView = (): void => undefined;
window.scrollTo = (): void => undefined;

configure({ asyncUtilTimeout: asyncUtilTimeoutInMilliseconds });

beforeAll(() => {
  server.listen({ onUnhandledRequest: 'error' });
});

afterEach(() => {
  toast.dismiss();
  cleanup();
  server.resetHandlers();
  resetFakeDatabase();
  localStorage.clear();
  document.documentElement.classList.add('dark');
});

afterAll(() => {
  server.close();
});
