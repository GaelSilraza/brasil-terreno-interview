import { setupServer } from 'msw/node';

import { fakeApiHandlers } from './fake-api/handlers';

export const server = setupServer(...fakeApiHandlers);
