# Statement Parser Design

Pipeline

Upload → Supabase Storage → Detect Type → Detect Bank → Select Parser →
Parse → Normalize → Duplicate Detection → Merchant Mapping → Categorize
→ Save

Interface

IStatementParser

Methods - CanParse() - Parse() - DetectBank()

Implementations

CsvParser PdfParserBase HdfcParser IciciParser SbiParser AxisParser

Merchant Learning

User correction creates MerchantMapping.

Future imports automatically apply mapping.

Hash

SHA256(UserId+AccountId+Date+Amount+Merchant)
