namespace SpendSense.Domain.Enums;

public enum AccountType { BankAccount = 1, CreditCard = 2, Wallet = 3, Cash = 4 }
public enum DebitCredit { Debit = 1, Credit = 2 }
public enum StatementParseStatus { Uploaded = 1, Processing = 2, Completed = 3, Failed = 4 }
public enum InsightPeriod { Monthly = 1, Quarterly = 2, Yearly = 3 }
public enum BudgetPeriod { Weekly = 1, Monthly = 2, Yearly = 3 }
public enum EmailSubscriptionType { MonthlyReport = 1, BudgetAlert = 2, ProductUpdates = 3 }
