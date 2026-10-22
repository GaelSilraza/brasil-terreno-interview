export type Result<TError, TValue> = [TError] | [undefined, TValue?];

export type PromiseResult<TError, TValue> = Promise<Result<TError, TValue>>;

export const toResultSync = <TError extends Error, TValue>(
  callbackOrValue: (() => TValue) | TValue
): Result<TError, TValue> => {
  try {
    const result =
      typeof callbackOrValue === 'function'
        ? (callbackOrValue as () => TValue)()
        : callbackOrValue;

    return [undefined, result as TValue];
  } catch (error) {
    return [error as TError];
  }
};

export const toResultAsync = async <TError extends Error, TValue>(
  callbackOrPromise: (() => Promise<TValue>) | Promise<TValue>
): PromiseResult<TError, TValue> => {
  try {
    const result =
      typeof callbackOrPromise === 'function'
        ? await (callbackOrPromise as () => Promise<TValue>)()
        : await callbackOrPromise;

    return [undefined, result as TValue];
  } catch (error) {
    return [error as TError];
  }
};
