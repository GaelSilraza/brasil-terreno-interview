using Finance.Shared.Domain;

namespace Finance.Modules.Accounts.Domain.Errors;

public static class AccountErrors
{
    public static readonly Error AccountNotFound = new("AccountNotFound", "The account was not found.", ErrorType.NotFound);

    public static readonly Error SourceAccountNotFound = new("SourceAccountNotFound", "The source account was not found.", ErrorType.NotFound);

    public static readonly Error DestinationAccountNotFound = new("DestinationAccountNotFound", "The destination account was not found.", ErrorType.NotFound);

    public static readonly Error DocumentNumberAlreadyExists = new("DocumentNumberAlreadyExists", "A person with this document number already exists.", ErrorType.Conflict);

    public static readonly Error SourceAccountInactive = new("SourceAccountInactive", "The source account is blocked or inactive.", ErrorType.BusinessRule);

    public static readonly Error DestinationAccountInactive = new("DestinationAccountInactive", "The destination account is blocked or inactive.", ErrorType.BusinessRule);

    public static readonly Error InsufficientFunds = new("InsufficientFunds", "The source account does not have enough balance and overdraft limit.", ErrorType.BusinessRule);

    public static readonly Error DestinationBalanceLimitExceeded = new("DestinationBalanceLimitExceeded", "The destination balance would exceed the maximum supported balance.", ErrorType.BusinessRule);

    public static readonly Error InvalidAmount = new("InvalidAmount", "The amount must be greater than zero.", ErrorType.Validation);

    public static readonly Error SameAccount = new("SameAccount", "Source and destination accounts must be different.", ErrorType.Validation);

    public static readonly Error InvalidOverdraftLimit = new("InvalidOverdraftLimit", "The overdraft limit cannot be negative.", ErrorType.Validation);

    public static readonly Error InvalidOpeningBalance = new("InvalidOpeningBalance", "The opening balance cannot exceed the overdraft limit.", ErrorType.Validation);
}
