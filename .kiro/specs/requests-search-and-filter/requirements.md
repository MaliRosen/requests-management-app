# מסמך דרישות — חיפוש וסינון בקשות (Requests Search & Filter)

## מבוא

מערכת ניהול הבקשות הקיימת חושפת endpoint יחיד — `GET /api/requests` — שמחזיר את כל הרשומות מהזיכרון ולאחר מכן מסנן בצד-שרת (in-memory). הפיצ'ר הנוכחי מרחיב את המערכת בשלושה ממדים:

**חלק א׳ — מימוש פיצ'ר:** הוספת חיפוש, סינון, מיון ועימוד (pagination) ברמת ה-DB, עם ממשק Angular מלא.
**חלק ב׳ — ארכיטקטורה:** מעבר ל-Microservices עם תקשורת אסינכרונית אמינה בין שירות הבקשות לשירות ההתראות.
**חלק ג׳ — פריסת ענן:** פריסה ב-Azure עם Compute, DB, Messaging, Monitoring ו-Scaling.

---

## מילון מונחים (Glossary)

| מונח | הגדרה |
|---|---|
| **API** | שכבת ה-`Requests.Api` — ה-ASP.NET Core Web API |
| **Application_Layer** | שכבת `Requests.Application` — לוגיקת עסקים ו-interfaces |
| **Repository** | `RequestRepository` — ממשה את `IRequestRepository` מול EF Core |
| **Service** | `RequestService` — ממשה את `IRequestService`, נקרא מה-Controller |
| **DB** | מסד הנתונים — EF Core SQLite (Dev, קובץ `requests.db`) / SQL Server (Prod) |
| **IQueryable** | ממשק EF Core המאפשר בניית שאילתות LINQ שמתורגמות ל-SQL ומבוצעות ב-DB |
| **Request** | ישות דומיין: `Id`, `RequestNumber`, `CustomerId`, `OwnerId`, `AssignedToUserId`, `Status`, `RequestType`, `CreatedAt`, `UpdatedAt` |
| **RequestStatus** | Enum: `New=1`, `InProgress=2`, `Completed=3`, `Cancelled=4` |
| **RequestType** | Enum: `General=1`, `Legal=2`, `Payment=3`, `Appeal=4` |
| **SearchQuery** | אובייקט query-parameters המכיל את כל פרמטרי הסינון, המיון והעימוד |
| **PagedResult** | אובייקט תגובה המכיל רשימת פריטים, מספר עמוד נוכחי, גודל עמוד וסך-הכל רשומות |
| **CurrentUser** | המשתמש המגיש את הבקשה; מזוהה דרך JWT token עם claims: `userId` ו-`isAdmin` |
| **Admin** | משתמש עם claim `isAdmin: true` — רשאי לראות את כל הבקשות |
| **RegularUser** | משתמש עם claim `isAdmin: false` — רשאי לראות רק בקשות שבהן `OwnerId == CurrentUser.Id` או `AssignedToUserId == CurrentUser.Id` |
| **Microservice** | שירות עצמאי עם מסד נתונים ו-deployment נפרדים |
| **MessageBroker** | Azure Service Bus — מתווך הודעות אסינכרוני |
| **OutboxPattern** | דפוס שמבטיח כי אירוע ופרסומו נשמרים באטומיות בטרנזקציה אחת |
| **NotificationService** | Microservice אחראי על שליחת התראות למשתמשים |
| **RequestsService** | Microservice אחראי על ניהול מחזור חיי הבקשות |
| **Frontend** | אפליקציית Angular המתממשקת עם ה-API |
| **FilterForm** | טופס סינון ב-Frontend — checkboxes לסטטוס/סוג, טקסט, תאריכים |
| **PaginationControls** | רכיב ניווט בין עמודים ב-Frontend |
| **EnumOptions** | ערכי הסינון hard-coded בקליינט כי הם enum קבוע בקוד. אם יעברו לניהול דינמי — יש להוסיף `GET /api/requests/metadata` |

---

## חלק א׳ — מימוש פיצ'ר (Feature Implementation)

---

### דרישה 1: סינון בקשות ברמת DB

**User Story:** כמשתמש (מנהל או משתמש רגיל), אני רוצה לסנן בקשות לפי קריטריונים שונים, כדי שאוכל למצוא את הבקשות הרלוונטיות מבלי לדפדף ידנית בכל הרשימה.

#### קריטריוני קבלה

1. WHEN הלקוח שולח `GET /api/requests` עם `requestNumber=<ערך>`, THE API SHALL מחזיר רק בקשות שבהן `RequestNumber` מכיל את הערך (case-insensitive, partial match).

2. WHEN הלקוח שולח `GET /api/requests` עם `status=<ערך אחד או יותר>`, THE API SHALL מחזיר רק בקשות שה-`Status` שלהן שייך לרשימת הערכים שסופקה.

3. WHEN הלקוח שולח `GET /api/requests` עם `requestType=<ערך אחד או יותר>`, THE API SHALL מחזיר רק בקשות שה-`RequestType` שלהן שייך לרשימת הערכים שסופקה.

4. WHEN הלקוח שולח `GET /api/requests` עם `createdFrom=<תאריך>`, THE API SHALL מחזיר רק בקשות שה-`CreatedAt` שלהן גדול מ-`createdFrom` או שווה לו.

5. WHEN הלקוח שולח `GET /api/requests` עם `createdTo=<תאריך>`, THE API SHALL מחזיר רק בקשות שה-`CreatedAt` שלהן קטן מ-`createdTo` או שווה לו.

6. WHEN הלקוח שולח `GET /api/requests` עם שילוב של מספר פרמטרי סינון, THE API SHALL מחיל את כל הפילטרים בו-זמנית (AND logic).

7. THE Repository SHALL בונה את שאילתת הסינון כ-`IQueryable<Request>` ומעביר אותה ל-DB לביצוע, ואינו טוען רשומות לזיכרון לפני הסינון.

---

### דרישה 2: אבטחה ואכיפת הרשאות

**User Story:** כמנהל מערכת, אני רוצה שהמערכת תאכוף הרשאות גישה לנתונים ברמת ה-DB, כדי שמשתמשים רגילים לא יוכלו לגשת לבקשות שאינן שלהם.

#### קריטריוני קבלה

1. WHEN CurrentUser הוא Admin, THE API SHALL מחזיר תוצאות ללא הגבלת ownership.

2. WHEN CurrentUser הוא RegularUser, THE API SHALL מחזיר רק בקשות שבהן `OwnerId == CurrentUser.Id` OR `AssignedToUserId == CurrentUser.Id`.

3. THE Repository SHALL מיישם את מגבלת ה-ownership כחלק מה-`IQueryable` לפני ביצוע השאילתה ב-DB.

4. IF ה-JWT token חסר, לא תקין, או פג תוקף, THEN THE API SHALL מחזיר HTTP 401 Unauthorized אוטומטית.

---

### דרישה 3: מיון תוצאות

**User Story:** כמשתמש, אני רוצה למיין את תוצאות החיפוש לפי עמודות שונות, כדי שאוכל לסדר את הרשימה בצורה הנוחה לי.

#### קריטריוני קבלה

1. WHEN הלקוח שולח `GET /api/requests` עם `sortBy=<שדה>` ו-`sortDirection=asc`, THE API SHALL ממיין את התוצאות בסדר עולה לפי השדה שצוין.

2. WHEN הלקוח שולח `GET /api/requests` עם `sortBy=<שדה>` ו-`sortDirection=desc`, THE API SHALL ממיין את התוצאות בסדר יורד לפי השדה שצוין.

3. WHERE `sortBy` ו/או `sortDirection` לא סופקו, THE API SHALL ממיין את התוצאות ב-`CreatedAt` בסדר יורד (ברירת מחדל).

4. THE API SHALL תומך במיון על-פי השדות: `Id`, `RequestNumber`, `Status`, `RequestType`, `CreatedAt`, `OwnerId`.

5. THE Repository SHALL מיישם את המיון כחלק מה-`IQueryable` לפני ביצוע השאילתה ב-DB.

---

### דרישה 4: עימוד (Pagination)

**User Story:** כמשתמש העובד עם מיליוני רשומות, אני רוצה שהמערכת תחזיר תוצאות בדפים, כדי שהביצועים יישארו גבוהים ללא קשר לגודל הנתונים.

#### קריטריוני קבלה

1. WHEN הלקוח שולח `GET /api/requests` עם `page=<מספר>` ו-`pageSize=<גודל>`, THE API SHALL מחזיר את הדף המתאים עם `pageSize` רשומות לכל היותר.

2. WHERE `page` לא סופק, THE API SHALL משתמש ב-`page=1` כברירת מחדל.

3. WHERE `pageSize` לא סופק, THE API SHALL משתמש ב-`pageSize=20` כברירת מחדל.

4. THE API SHALL מחזיר `PagedResult` הכולל: `items` (רשימת `RequestDto`), `page` (מספר עמוד נוכחי), `pageSize` (גודל עמוד), `totalCount` (סך הרשומות התואמות לפני העימוד).

5. THE Repository SHALL מיישם `Skip` ו-`Take` כחלק מה-`IQueryable` לפני ביצוע השאילתה ב-DB.

6. IF `page` קטן מ-1 או `pageSize` קטן מ-1 או `pageSize` גדול מ-200, THEN THE API SHALL מחזיר HTTP 400 Bad Request עם מסר שגיאה מפורש.

---

### דרישה 4א: ביצועים — אינדקסים ונתוני פיתוח

**User Story:** כמפתח, אני רוצה שסביבת הפיתוח תשקף תנאי ביצועים ריאליים, כדי שאוכל לאמת שה-IQueryable pipeline עובד נכון על כמות נתונים משמעותית.

#### קריטריוני קבלה

1. THE DbContext SHALL מגדיר אינדקסים על השדות: `Status`, `RequestType`, `CreatedAt`, `RequestNumber`, `AssignedToUserId`.

2. THE DbContext SHALL מגדיר Composite Index על `(OwnerId, CreatedAt)` — מייעל את הקייס הנפוץ ביותר: משתמש רגיל מסנן לפי ownership ואז ממיין לפי תאריך.

3. THE DbSeeder SHALL יוצר לפחות 100,000 רשומות מדומות בסביבת הפיתוח, בhatch של 1,000 כדי לא לטעון הכל לזיכרון בבת אחת.

4. THE Infrastructure SHALL משתמש ב-SQLite (קובץ `requests.db`) בסביבת הפיתוח, כך שהאינדקסים פועלים בפועל ולא מתעלמים.

5. WHERE המערכת עוברת לפרודקשן, THE Infrastructure SHALL מאפשר החלפת SQLite ב-SQL Server בשינוי שורה אחת ב-`DependencyInjection.cs`, ללא שינוי בשאר הקוד.

---

### דרישה 5: ולידציה של קלט

**User Story:** כמפתח שמשלב עם ה-API, אני רוצה לקבל הודעות שגיאה ברורות כאשר שולחים ערכים לא תקינים, כדי שאוכל לזהות ולתקן בעיות במהירות.

#### קריטריוני קבלה

1. IF `status` מכיל ערך שאינו חוקי ב-enum `RequestStatus`, THEN THE API SHALL מחזיר HTTP 400 Bad Request עם מסר הכולל את הערכים החוקיים המקובלים.

2. IF `requestType` מכיל ערך שאינו חוקי ב-enum `RequestType`, THEN THE API SHALL מחזיר HTTP 400 Bad Request עם מסר הכולל את הערכים החוקיים המקובלים.

3. IF `createdFrom` או `createdTo` אינם בפורמט תאריך תקין (ISO 8601), THEN THE API SHALL מחזיר HTTP 400 Bad Request עם מסר שגיאה מפורש המציין את השדה הבעייתי.

4. IF `createdFrom` גדול מ-`createdTo` (כאשר שניהם סופקו), THEN THE API SHALL מחזיר HTTP 400 Bad Request עם מסר שגיאה מפורש.

5. IF `sortBy` מכיל שם שדה שאינו נתמך, THEN THE API SHALL מחזיר HTTP 400 Bad Request עם רשימת השדות הנתמכים.

6. IF `sortDirection` מכיל ערך שאינו `asc` ואינו `desc` (case-insensitive), THEN THE API SHALL מחזיר HTTP 400 Bad Request.

---

### דרישה 6: ממשק משתמש — טופס סינון (Frontend)

**User Story:** כמשתמש קצה, אני רוצה ממשק ויזואלי לסינון הבקשות, כדי שלא אצטרך לכתוב query-strings ידנית.

#### קריטריוני קבלה

1. THE Frontend SHALL מציג FilterForm הכולל: שדה טקסט לחיפוש לפי `RequestNumber`, קבוצת checkboxes לסינון לפי `Status` (ניתן לבחור מרובים), קבוצת checkboxes לסינון לפי `RequestType` (ניתן לבחור מרובים), ו-date pickers לטווח `createdFrom`/`createdTo`.

1a. THE Frontend SHALL מציג כפתור "נקה סינון" שמאפס את כל שדות הטופס בבת אחת.

2. WHEN המשתמש משנה ערך כלשהו ב-FilterForm, THE Frontend SHALL שולח בקשה חדשה ל-API עם הפרמטרים המעודכנים תוך 500ms לאחר הפסקת הקלדה (debounce).

3. WHILE בקשה ל-API נמצאת בתהליך, THE Frontend SHALL מציג spinner טעינה ומנטרל את אפשרות שליחת בקשה נוספת.

4. IF ה-API מחזיר שגיאה, THEN THE Frontend SHALL מציג הודעת שגיאה ברורה למשתמש ואינו מציג טבלת תוצאות.

5. WHEN ה-API מחזיר רשימה ריקה, THE Frontend SHALL מציג הודעה "לא נמצאו בקשות התואמות לחיפוש".

---

### דרישה 7: ממשק משתמש — טבלת תוצאות ומיון (Frontend)

**User Story:** כמשתמש, אני רוצה לראות תוצאות בטבלה ולמיין לפי כל עמודה בלחיצה, כדי שאוכל לנתח את הנתונים בנוחות.

#### קריטריוני קבלה

1. THE Frontend SHALL מציג טבלת תוצאות עם העמודות: `Id`, `RequestNumber`, `Status`, `RequestType`, `CreatedAt`, `OwnerId`. ערכי `Status` ו-`RequestType` יוצגו כתוויות קריאות בעברית (לא כמספרים). עמודת `CreatedAt` תוצג בפורמט `dd/MM/yyyy`.

2. WHEN המשתמש לוחץ על כותרת עמודה שניתנת למיון, THE Frontend SHALL שולח בקשה חדשה ל-API עם `sortBy=<שם העמודה>` ו-`sortDirection=asc`.

3. WHEN המשתמש לוחץ שנית על אותה עמודה, THE Frontend SHALL מחליף את `sortDirection` ל-`desc` ושולח בקשה חדשה.

4. THE Frontend SHALL מציג אינדיקטור ויזואלי (חץ למעלה/למטה) על העמודה הממוינת כרגע.

---

### דרישה 8: ממשק משתמש — עימוד (Frontend)

**User Story:** כמשתמש, אני רוצה לנווט בין עמודי תוצאות, כדי שאוכל לגלוש ברשימות ארוכות.

#### קריטריוני קבלה

1. THE Frontend SHALL מציג PaginationControls הכוללים: כפתור "הקודם", כפתור "הבא", מספר העמוד הנוכחי, וסך הרשומות.

2. WHEN המשתמש לוחץ "הבא" או "הקודם", THE Frontend SHALL עדכן את `page` בהתאם ושלח בקשה חדשה ל-API.

3. WHILE המשתמש נמצא בעמוד הראשון, THE Frontend SHALL מנטרל את כפתור "הקודם".

4. WHILE המשתמש נמצא בעמוד האחרון, THE Frontend SHALL מנטרל את כפתור "הבא".

5. WHEN המשתמש משנה פרמטר סינון, THE Frontend SHALL מאפס את `page` ל-1 לפני שליחת הבקשה.

---

### דרישה 9: בדיקות אוטומטיות — Backend

**User Story:** כמפתח, אני רוצה בדיקות אוטומטיות מקיפות לשכבת ה-Service וה-Repository, כדי למנוע רגרסיות.

#### קריטריוני קבלה

1. THE Test_Suite SHALL כוללת בדיקות unit ל-`RequestService` המכסות: סינון לפי כל פרמטר בנפרד, שילוב פרמטרים, הרשאות Admin, הרשאות RegularUser, ומיון.

2. THE Test_Suite SHALL כוללת בדיקות integration ל-`RequestRepository` המכסות: שאילתות `IQueryable` מסתיימות ב-DB (לא בזיכרון).

3. FOR ALL שאילתות סינון תקינות, THE Service SHALL מחזיר רק רשומות התואמות לכלל הפרמטרים שסופקו (invariant: סינון משולב).

4. FOR ALL ערכי `page` ו-`pageSize` תקינים, THE Service SHALL מחזיר `PagedResult` שבו `items.Count <= pageSize` ו-`totalCount` שווה למספר הרשומות התואמות לפני העימוד (round-trip property).

---

## חלק ב׳ — ארכיטקטורה: מעבר ל-Microservices

---

### דרישה 10: פירוק ל-Microservices

**User Story:** כארכיטקט, אני רוצה לתכנן פירוק המערכת ל-Microservices עצמאיים, כדי שכל domain יוכל להיפרס, להתקדם ולהיות מאוחסן בנפרד.

#### קריטריוני קבלה

1. THE Architecture SHALL מגדיר חמישה Microservices עצמאיים: `CustomersService`, `RequestsService`, `NotificationService`, `DocumentsService`, `ReportingService`.

2. THE Architecture SHALL מבטיח שכל Microservice מחזיק ב-DB משלו ואינו חולק schema עם Microservice אחר (database-per-service).

3. THE Architecture SHALL מגדיר API Gateway שמנתב בקשות חיצוניות אל ה-Microservices המתאימים.

4. WHEN RequestsService מעדכן את ה-DB שלו, THE RequestsService SHALL פרסם Domain Event ל-MessageBroker באותה טרנזקציה (Outbox Pattern).

---

### דרישה 11: תקשורת אסינכרונית אמינה — Outbox Pattern

**User Story:** כארכיטקט, אני רוצה שתקשורת בין שירותים תהיה אמינה גם כאשר NotificationService אינו זמין, כדי שלא יאבדו אירועים.

#### קריטריוני קבלה

1. WHEN בקשה חדשה נוצרת או שה-Status שלה משתנה, THE RequestsService SHALL שומר Domain Event בטבלת `OutboxMessages` באותה טרנזקציה DB כמו שינוי הנתונים.

2. WHEN טרנזקציה DB מסתיימת בהצלחה, THE OutboxPublisher SHALL פרסם את ה-Domain Events הממתינים ל-MessageBroker.

3. IF MessageBroker אינו זמין, THEN THE OutboxPublisher SHALL ינסה לפרסם שוב בפרקי זמן קצובים עד להצלחה, ואינו גורם לאובדן נתונים.

4. WHEN NotificationService מקבל הודעה מה-MessageBroker, THE NotificationService SHALL מעבד את ההודעה ושולח התראה ל-RegularUser הרלוונטי.

5. IF NotificationService מקבל אותה הודעה פעמיים (כתוצאה מ-retry), THEN THE NotificationService SHALL מעבד אותה פעם אחת בלבד (idempotent processing).

---

### דרישה 12: תיעוד ארכיטקטורה

**User Story:** כמפתח בצוות, אני רוצה תיעוד מפורט של ארכיטקטורת ה-Microservices, כדי שאוכל להבין את זרימות הנתונים ולבצע שינויים בבטחה.

#### קריטריוני קבלה

1. THE Design_Document SHALL כולל diagram של תקשורת בין כל ה-Microservices (מי מדבר עם מי, sync או async).

2. THE Design_Document SHALL מתאר את Outbox Pattern צעד-אחר-צעד: כתיבה לטבלת Outbox, background publisher, שליחה ל-MessageBroker, קבלה ב-NotificationService.

3. THE Design_Document SHALL מסביר את הסיכונים של תקשורת sync ישירה בין שירותים ואת היתרונות של messaging אסינכרוני לתרחיש זה.

---

## חלק ג׳ — פריסת ענן (Cloud Deployment — Azure)

---

### דרישה 13: Compute

**User Story:** כ-DevOps Engineer, אני רוצה לפרוס את המערכת על Azure עם יכולות Auto-Scaling, כדי שהמערכת תתמודד עם עומסים משתנים.

#### קריטריוני קבלה

1. THE Cloud_Architecture SHALL כולל פריסת ה-Microservices על Azure Kubernetes Service (AKS) או Azure Container Apps.

2. THE Cloud_Architecture SHALL מגדיר Horizontal Pod Autoscaling (HPA) שמסנן כל Microservice באופן עצמאי לפי עומס CPU/Memory.

3. THE Cloud_Architecture SHALL כולל Azure API Management (APIM) כ-API Gateway חיצוני מול ה-Microservices.

---

### דרישה 14: מסד נתונים

**User Story:** כ-DevOps Engineer, אני רוצה שכל Microservice ישתמש ב-managed database service מתאים ב-Azure, כדי להבטיח ביצועים, זמינות גבוהה וגיבויים אוטומטיים.

#### קריטריוני קבלה

1. THE Cloud_Architecture SHALL מגדיר Azure SQL Database (או Azure PostgreSQL) עבור `RequestsService` ו-`CustomersService`, עם Geo-Redundant Backup.

2. THE Cloud_Architecture SHALL מגדיר שכל Microservice מחזיק ב-DB נפרד שאינו נגיש ישירות על-ידי שירותים אחרים.

3. WHERE `RequestsService` דורש Outbox Pattern, THE Cloud_Architecture SHALL מבטיח שטבלת `OutboxMessages` נמצאת באותו DB כמו הנתונים העסקיים.

---

### דרישה 15: Messaging

**User Story:** כ-DevOps Engineer, אני רוצה להשתמש ב-managed messaging service ב-Azure, כדי שהתקשורת האסינכרונית בין שירותים תהיה אמינה ומדידה.

#### קריטריוני קבלה

1. THE Cloud_Architecture SHALL משתמש ב-Azure Service Bus עם Topics ו-Subscriptions לניתוב Domain Events בין Microservices.

2. THE Cloud_Architecture SHALL מגדיר Dead Letter Queue לכל Subscription, כדי לתפוס הודעות שנכשלו לאחר מספר מרבי של ניסיונות.

3. THE Cloud_Architecture SHALL מגדיר את `NotificationService` כ-subscriber ל-Topic של `RequestsService` עם סינון לפי סוג האירוע.

---

### דרישה 16: ניטור ו-Observability

**User Story:** כ-SRE, אני רוצה לנטר את בריאות המערכת ולקבל התראות, כדי שאוכל לזהות ולטפל בתקלות לפני שהן משפיעות על משתמשים.

#### קריטריוני קבלה

1. THE Cloud_Architecture SHALL כולל Azure Monitor ו-Application Insights לאיסוף logs, metrics ו-traces מכל ה-Microservices.

2. THE Cloud_Architecture SHALL מגדיר Distributed Tracing עם correlation IDs המאפשר מעקב אחר בקשה לאורך מספר Microservices.

3. THE Cloud_Architecture SHALL מגדיר Alerts על: שיעור שגיאות HTTP > 1%, זמן תגובה > 2 שניות (P95), כשל ב-OutboxPublisher.

4. THE Cloud_Architecture SHALL כולל Azure Key Vault לניהול secrets (connection strings, API keys) שאינם נשמרים ב-configuration files.

---

### דרישה 17: אבטחה ותשתית רשת

**User Story:** כ-Security Engineer, אני רוצה שהמערכת תוגן בשכבות ברמת הרשת ואפליקציה ב-Azure, כדי לצמצם את שטח ההתקפה.

#### קריטריוני קבלה

1. THE Cloud_Architecture SHALL מגדיר Azure Virtual Network (VNet) שבו Microservices מתקשרים פנימית ואינם חשופים ישירות לאינטרנט.

2. THE Cloud_Architecture SHALL מגדיר Network Security Groups (NSG) המגבילים תנועה נכנסת ל-Microservices ממקורות מאושרים בלבד.

3. THE Cloud_Architecture SHALL משתמש ב-Managed Identity לאימות שירותים Azure-to-Azure ומגביל שימוש ב-connection strings עם secrets.

