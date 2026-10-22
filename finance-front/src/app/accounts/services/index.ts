import { create } from './create';
import { findAll } from './findAll';
import { findById } from './findById';
import { findEntries } from './findEntries';
import { updateOverdraftLimit } from './updateOverdraftLimit';
import { updateStatus } from './updateStatus';

export const accountsService = {
  findAll,
  findById,
  create,
  findEntries,
  updateOverdraftLimit,
  updateStatus,
};
