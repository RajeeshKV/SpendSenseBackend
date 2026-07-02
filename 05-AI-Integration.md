# AI Integration

Providers

1.  Groq (preferred)
2.  Google AI Studio
3.  OpenRouter

Create abstraction

IAiService

Methods

GenerateInsights() AnalyzeTransactions() Chat()

Only send aggregated data.

Prompt includes: - Spending by category - Merchant totals - Monthly
trends - Recurring payments

Never send account numbers or personal identifiers.

Cache monthly insights in database.
