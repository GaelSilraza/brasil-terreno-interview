export const defaultApiErrorMessage =
  'Não foi possível concluir a operação. Tente novamente.';

export const apiErrorMessages: Record<string, string> = {
  ValidationFailed: 'Dados inválidos. Verifique os campos destacados.',
  InvalidAmount: 'O valor deve ser maior que zero.',
  SameAccount: 'A conta de origem e a de destino devem ser diferentes.',
  InvalidOverdraftLimit: 'O limite de cheque especial não pode ser negativo.',
  InvalidOpeningBalance:
    'O saldo inicial não pode ultrapassar o limite de cheque especial.',
  ScheduleDateMustBeInFuture: 'A data do agendamento deve ser futura.',
  AccountNotFound: 'Conta não encontrada.',
  SourceAccountNotFound: 'Conta de origem não encontrada.',
  DestinationAccountNotFound: 'Conta de destino não encontrada.',
  TransferNotFound: 'Transferência não encontrada.',
  DocumentNumberAlreadyExists: 'Já existe uma pessoa cadastrada com este CPF.',
  TransferCannotBeCancelled:
    'Apenas transferências agendadas podem ser canceladas.',
  InvalidStatusTransition: 'A transferência não pode mudar para este status.',
  Conflict: 'Conflito ao salvar os dados. Tente novamente.',
  SourceAccountInactive: 'A conta de origem está bloqueada ou inativa.',
  DestinationAccountInactive: 'A conta de destino está bloqueada ou inativa.',
  InsufficientFunds: 'Saldo e cheque especial insuficientes.',
  DestinationBalanceLimitExceeded:
    'O saldo da conta de destino ultrapassaria o máximo permitido.',
  AttemptLimitExceeded: 'Limite de tentativas por hora excedido.',
  HourlyAmountLimitExceeded: 'Limite de valor por hora excedido.',
  IdempotencyKeyReused: 'Esta requisição já foi enviada com dados diferentes.',
  ProcessingTimeout: 'O processamento da transferência expirou.',
  UnexpectedError: 'Ocorreu um erro inesperado. Tente novamente.',
};
