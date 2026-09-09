# Design Document

## סקירה כללית

מסמך זה מכסה שלושה חלקים:
- **חלק א׳**: תכנון טכני לפיצ'ר החיפוש והסינון (Backend + Angular Frontend)
- **חלק ב׳**: תכנון ארכיטקטורה למעבר ל-Microservices (הסבר + תרשים)
- **חלק ג׳**: סקיצת פריסה ב-Azure (הסבר + תרשים)

---

## חלק א׳ — תכנון מימוש הפיצ'ר

### ארכיטקטורת Backend

הארכיטקטורה השכבתית הקיימת נשמרת ומורחבת:

```
┌─────────────────────────────────────────┐
│           Requests.Api                  │
│  RequestsController                     │
│  - GET /api/requests?[query params]     │
└────────────────┬────────────────────────┘
                 │ קורא ל-
┌────────────────▼────────────────────────┐
│        Requests.Application             │
│  IRequestService / RequestService       │
│  SearchRequestsQuery (פרמטרי חיפוש)    │
│  PagedResult<T> (תגובה מעומדת)          │
└────────────────┬────────────────────────┘
                 │ קורא ל-
┌────────────────▼────────────────────────┐
│        Requests.Infrastructure          │
│  IRequestRepository / RequestRepository │
│  - בונה IQueryable עם כל הפילטרים      │
│  - מיישם ownership filter               │
│  - מיישם מיון + skip + take             │
│  - מבצע CountAsync + ToListAsync        │
└────────────────┬────────────────────────┘
                 │
┌────────────────▼────────────────────────┐
│         EF Core / SQLite (Dev)          │
│  קובץ requests.db — אינדקסים פועלים   │
│  כל הסינון/מיון/עימוד קורה ב-SQL       │
└─────────────────────────────────────────┘
```

### חוזים חדשים (Data Contracts)

**SearchRequestsQuery** — פרמטרי query שנשלחים מהלקוח:
```csharp
public sealed record SearchRequestsQuery(
    string? RequestNumber,       // חיפוש חלקי
    RequestStatus[]? Status,     // ערכים מרובים
    RequestType[]? RequestType,  // ערכים מרובים
    DateTime? CreatedFrom,
    DateTime? CreatedTo,
    string SortBy = "CreatedAt",
    string SortDirection = "desc",
    int Page = 1,
    int PageSize = 20
);
```

**PagedResult<T>** — עטיפת תגובה:
```csharp
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount
);
```

### Repository — שרשרת IQueryable

ה-Repository בונה את השאילתה בסדר הבא — הכל רץ ב-DB:

```
1. התחלה:      _db.Requests.AsQueryable()
2. Ownership:  WHERE OwnerId == userId OR AssignedToUserId == userId  (דלוג על Admin)
3. מספר בקשה: WHERE RequestNumber.Contains(value)
4. סטטוס:     WHERE Status IN (...)
5. סוג בקשה:  WHERE RequestType IN (...)
6. מתאריך:    WHERE CreatedAt >= createdFrom
7. עד תאריך:  WHERE CreatedAt <= createdTo
8. ספירה:     SELECT COUNT(*) → totalCount
9. מיון:      ORDER BY {sortBy} {ASC|DESC}
10. עימוד:    SKIP (page-1)*pageSize  TAKE pageSize
11. ביצוע:    ToListAsync()
```

### אינדקסים ב-DbContext

כדי לתמוך בביצועים על מיליוני רשומות, `RequestsDbContext.OnModelCreating` מגדיר:

```csharp
entity.HasIndex(r => r.Status);                          // סינון לפי סטטוס
entity.HasIndex(r => r.RequestType);                     // סינון לפי סוג
entity.HasIndex(r => r.CreatedAt);                       // מיון ברירת מחדל
entity.HasIndex(r => r.RequestNumber);                   // חיפוש חלקי
entity.HasIndex(r => r.AssignedToUserId);                // חצי מה-ownership filter
entity.HasIndex(r => new { r.OwnerId, r.CreatedAt });    // composite — ownership + sort
```

ה-Composite Index על `(OwnerId, CreatedAt)` מייעל את הקייס הנפוץ ביותר: משתמש רגיל מסנן לפי `OwnerId` ואז ממיין לפי `CreatedAt`. ה-DB נכנס לאינדקס לפי `OwnerId` ומקבל תת-קבוצה כבר ממוינת לפי תאריך — בלי Full Table Scan.

**הערה:** ב-SQLite (Dev) האינדקסים יוצרים בפועל דרך `EnsureCreated()` ב-`Program.cs`. ב-InMemory הם מתעלמים לגמרי — לכן עברנו ל-SQLite.

### ולידציית קלט

הולידציה קורית ב-Controller דרך model binding + בדיקות ידניות:
- ערכי enum לא תקינים ב-`status` או `requestType` → 400 עם הערכים המותרים
- `createdFrom > createdTo` → 400
- `page < 1` או `pageSize < 1` או `pageSize > 200` → 400
- שדה `sortBy` לא מוכר → 400 עם רשימת השדות המותרים
- JWT token חסר או לא תקין → 401 (מטופל אוטומטית על ידי `[Authorize]`)

### תכנון Angular Frontend (Standalone Architecture)

```
src/app/
├── models/
│   ├── request.model.ts          # ממשק RequestDto + enums
│   ├── paged-result.model.ts     # ממשק PagedResult<T>
│   └── search-query.model.ts     # ממשק SearchQuery
├── services/
│   └── requests.service.ts       # קריאות HttpClient ל-API (providedIn: 'root')
├── components/
│   ├── search-filter/            # standalone: checkboxes, טקסט, תאריכים, נקה
│   ├── requests-table/           # standalone: טבלה, מיון, labels בעברית, date pipe
│   └── pagination/               # standalone: הבא/הקודם, ספירה
└── app.ts                        # standalone root: מתאם, state, bootstrap
```

**מדוע Standalone ולא NgModule:**
Angular 17+ ממליץ על standalone. כל קומפוננטה מגדירה את ה-dependencies שלה ב-`imports` — אין מודול מרכזי שמנהל הצהרות. זה מקל על הבנת התלויות ומקצר את ה-boilerplate.

**זרימת נתונים:**
```
המשתמש מסמן checkbox / מקליד / בוחר תאריך ב-SearchFilterComponent
    → debounce 500ms (שדה טקסט) / מיידי (checkboxes, תאריכים)
    → App מקבל filterChange, מאפס page ל-1
    → קורא ל-RequestsService.search(query)
    → מציג spinner טעינה
    → בהצלחה: מעביר נתונים ל-RequestsTableComponent + PaginationComponent
    → בשגיאה: מציג הודעת שגיאה
    → ברשימה ריקה: מציג "לא נמצאו בקשות התואמות לחיפוש"
```

**טופס סינון:**
- checkboxes לסטטוס וסוג בקשה — ניתן לבחור מרובים בלחיצה ישירה, ללא Ctrl+Click
- ערכי checkbox מנוהלים ב-`Set<RequestStatus>` ו-`Set<RequestType>` — ערכים מספריים מובטחים (ללא בעיית string casting של native select)
- כפתור "נקה סינון" מאפס את כל השדות בבת אחת
- ערכי הסינון hard-coded בקליינט כי `RequestStatus` ו-`RequestType` הם enum קבוע בקוד — שינויהם ממילא מצריך פריסה מחדש. אם בעתיד יעברו לניהול דינמי, הגישה הנכונה היא `GET /api/requests/metadata` שהקליינט יקרא בהפעלה.

**טבלה:**
- `statusLabels` ו-`typeLabels` — Record שממפה מספר enum לשם תצוגה בעברית
- `| date:'dd/MM/yyyy'` — Angular DatePipe מעצב את `createdAt`
- כותרות עמודות בעברית

**מיון:**
- לחיצה על כותרת עמודה קובעת `sortBy` ומחליפה `sortDirection` (asc ↔ desc)
- עמודה ממוינת מציגה ▲ או ▼
- שינוי מיון מפעיל קריאת API חדשה

**עימוד:**
- כפתורי הבא/הקודם מעדכנים `page` ומפעילים קריאת API
- הקודם מנוטרל בעמוד 1, הבא מנוטרל כש-`page * pageSize >= totalCount`
- כל שינוי פילטר מאפס `page` ל-1

---

## חלק ב׳ — ארכיטקטורת Microservices

### למה Microservices?

כשהמערכת גדלה עם דומיינים של Customers, Requests, Notifications, Documents ו-Reporting — מונוליט הופך קשה לפריסה ולסקיילינג עצמאי. כל דומיין יכול:
- להיפרס בנפרד (שינוי קטן לא מצריך פריסה של הכל)
- להיסקל בנפרד (Reporting זקוק ל-CPU, Notifications זקוק ל-throughput)
- להיות בבעלות צוות נפרד

### מפת השירותים

```
                        ┌─────────────────┐
                        │    API Gateway   │
                        │  (Azure APIM)   │
                        └────────┬────────┘
                                 │ מנתב בקשות
          ┌──────────────────────┼──────────────────────┐
          │                      │                      │
┌─────────▼──────┐    ┌──────────▼──────┐    ┌─────────▼───────┐
│CustomersService│    │RequestsService  │    │DocumentsService │
│  [SQL DB]      │    │  [SQL DB]       │    │  [Blob Storage] │
└────────────────┘    └────────┬────────┘    └─────────────────┘
                               │
                    מפרסם Domain Events
                    (RequestCreated, StatusChanged)
                               │
                    ┌──────────▼──────────┐
                    │  Azure Service Bus  │
                    │  Topic: req-events  │
                    └──────┬──────────────┘
                           │ נרשמים
              ┌────────────┼────────────┐
              │                         │
   ┌──────────▼──────┐       ┌──────────▼──────┐
   │NotificationSvc  │       │ReportingService │
   │  [DB נפרד]      │       │  [DB/DW נפרד]  │
   └──────────────────┘      └──────────────────┘
```

### תקשורת אמינה — Outbox Pattern

**הבעיה:** אם RequestsService כותב ל-DB ואז מנסה לפרסם ל-Service Bus — ואז Service Bus לא זמין — האירוע אובד. או אם האפליקציה קורסת בין שתי הפעולות — יש אי-עקביות.

**הפתרון — Outbox Pattern:**

```
שלב 1: RequestsService מקבל "צור בקשה" או "שנה סטטוס"
    ↓
שלב 2: בטרנזקציה DB אחת:
    - INSERT לטבלת Requests (הנתונים העסקיים)
    - INSERT לטבלת OutboxMessages (האירוע לפרסום)
    → שניהם מצליחים או שניהם נכשלים. אין אי-עקביות.
    ↓
שלב 3: תהליך רקע (OutboxPublisher) רץ כל כמה שניות:
    - SELECT הודעות לא-מעובדות FROM OutboxMessages
    - פרסם כל אחת ל-Azure Service Bus
    - אם הפרסום הצליח → סמן כ-"Published"
    - אם Service Bus לא זמין → נסה שוב עם exponential backoff (אין אובדן נתונים)
    ↓
שלב 4: NotificationService מקבל הודעה מ-Service Bus:
    - בדוק אם MessageId כבר עובד (טבלת idempotency)
    - אם כן → דלג (מונע התראה כפולה)
    - אם לא → שלח התראה → סמן MessageId כמעובד
```

**למה זה אמין:**
- אם Service Bus לא זמין → אירועים מחכים ב-OutboxMessages עד שיחזור
- אם האפליקציה קורסת אחרי כתיבה ל-DB → OutboxPublisher יאסוף הודעות לא-מעובדות בהפעלה הבאה
- אם NotificationService מעבד אותו אירוע פעמיים → idempotency key מונע התראות כפולות

**למה לא קריאת HTTP ישירה בין שירותים:**
- אם NotificationService לא זמין, קריאת HTTP מ-RequestsService תיכשל → יצירת הבקשה עצמה תיכשל
- עם messaging אסינכרוני, RequestsService לא תלוי בזמינות NotificationService — הוא כותב ל-Outbox וממשיך

---

## חלק ג׳ — פריסת ענן (Azure)

### סקיצת ארכיטקטורה

```
אינטרנט
    │
    ▼
┌───────────────────────────────────────────┐
│       Azure API Management (APIM)         │
│  - הגבלת קצב, אימות, ניתוב              │
└────────────────────┬──────────────────────┘
                     │ HTTPS (VNet פנימי)
┌────────────────────▼──────────────────────┐
│         Azure Container Apps              │
│  ┌─────────────┐  ┌─────────────┐         │
│  │  Requests   │  │  Customers  │  ...    │
│  │  Service    │  │  Service    │         │
│  └──────┬──────┘  └─────────────┘         │
│         │  Auto-scale (min 1, max 10)      │
└─────────┼─────────────────────────────────┘
          │
    ┌─────┴──────────────────────────┐
    │                                │
    ▼                                ▼
┌──────────────┐           ┌────────────────────┐
│ Azure SQL DB │           │ Azure Service Bus   │
│ (לכל שירות) │           │ Topic: req-events   │
│ גיבוי גאו.  │           │ - notification-sub  │
└──────────────┘           │ - reporting-sub     │
                           │ Dead Letter Queue   │
                           └────────────────────┘
                                    │
                           ┌────────▼────────┐
                           │NotificationSvc  │
                           │(Container Apps) │
                           └─────────────────┘

רכיבים חוצי-מערכת:
┌──────────────────────────────────────────┐
│ Azure Monitor + Application Insights     │
│ - Distributed tracing (correlation IDs) │
│ - התראות: שיעור שגיאות >1%, P95 >2s    │
│ - Dashboard לכל שירות                   │
└──────────────────────────────────────────┘
┌──────────────────────────────────────────┐
│ Azure Key Vault                          │
│ - connection strings ל-DB               │
│ - מפתחות Service Bus                    │
│ - Managed Identity (ללא secrets בקוד)   │
└──────────────────────────────────────────┘
┌──────────────────────────────────────────┐
│ Azure Virtual Network (VNet)             │
│ - שירותים מתקשרים פנימית בלבד           │
│ - רק APIM חשוף לאינטרנט                 │
│ - כללי NSG: חסום הכל חוץ מפורטים מורשים│
└──────────────────────────────────────────┘
```

### טבלת החלטות

| רכיב | בחירה | סיבה |
|---|---|---|
| Compute | Azure Container Apps | פשוט יותר מ-AKS לסקיילינג הזה, auto-scale מובנה |
| Database | Azure SQL Database | Managed, גיבוי גאוגרפי, תואם ל-EF Core |
| Messaging | Azure Service Bus | אמין, תומך Topics/Subscriptions, Dead Letter Queue |
| ניטור | Application Insights | אינטגרציה עמוקה עם .NET, distributed tracing מהקופסה |
| Secrets | Azure Key Vault + Managed Identity | ללא secrets בקוד או בקבצי config |
| Gateway | Azure API Management | הגבלת קצב, אימות, ניתוב, גרסאות API |

### סקיילינג

- כל Container App מסתקל בנפרד לפי מספר בקשות HTTP או CPU
- מנוי ל-Service Bus מפעיל סקיילינג של NotificationService (מסתקל כשהתור גדל)
- Azure SQL מסתקל אנכית (DTUs/vCores) לכל שירות בנפרד
