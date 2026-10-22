import { cancel } from './cancel';
import { create } from './create';
import { findByAccount } from './findByAccount';
import { findById } from './findById';
import { findLimits } from './findLimits';
import { schedule } from './schedule';
import { updateLimits } from './updateLimits';

export const transfersService = {
  create,
  schedule,
  findById,
  cancel,
  findByAccount,
  findLimits,
  updateLimits,
};
