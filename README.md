# Requests Search & Filter

Full-stack feature: חיפוש, סינון, מיון ועימוד ברמת ה-DB על ASP.NET Core 8 API עם Angular 17 frontend.

---

## סטטוס

הפרויקט נבדק ורץ במלואו:
- ✅ 16 בדיקות backend — כולן עוברות
- ✅ Backend רץ על `http://localhost:5000`
- ✅ Frontend רץ על `http://localhost:4200`
- ✅ תקשורת מלאה בין frontend ל-backend אומתה

---

## הרצת Backend

```bash
dotnet run --project server/src/Requests.Api
```

API זמין ב-`http://localhost:5000`.

בהפעלה ראשונה: יוצר קובץ `requests.db` (SQLite) עם 100,000 רשומות מדומות.

---

## הרצת Frontend

```bash
cd frontend
npm install
ng serve
```

Frontend זמין ב-`http://localhost:4200`.

---

## הרצת בדיקות

```bash
dotnet test server/tests/Requests.Tests/Requests.Tests.csproj
```

לצפייה בשם כל בדיקה בנפרד:

```bash
dotnet test server/tests/Requests.Tests/Requests.Tests.csproj --logger "console;verbosity=normal"
```

16 בדיקות unit ל-`RequestService`: סינון, הרשאות, מיון, pagination, שילוב פילטרים, עמוד 2, תוצאה ריקה. רצות in-memory ללא DB.

---

## הרצת בדיקות Frontend

```bash
cd frontend
ng test --watch=false
```

10 בדיקות unit ל-`SearchFilterComponent` (Karma + Jasmine): אתחול טופס, לחיצת חפש, סימון/ביטול status, ולידציית טווח תאריכים (כולל גבול ותיקון שגיאה), סינון משולב, טופס ריק, ניקוי פילטרים.

---

## תכונות Frontend

- **טופס סינון** — חיפוש לפי מספר בקשה, סטטוס (checkboxes מרובים), סוג בקשה, טווח תאריכים
- **ולידציה בזמן אמת** — שגיאת טווח תאריכים מוצגת מיד עם השינוי, כפתור "חפש" מנוטרל בזמן שגיאה
- **טבלת תוצאות** — מיון לפי כל עמודה בלחיצה, חץ כיוון, תוויות בעברית לסטטוס וסוג
- **Pagination** — ניווט בין עמודים, מוסתרת כש-0 תוצאות
- **חיווי טעינה** — spinner בזמן קריאה לשרת
- **חיווי שגיאה** — הודעת שגיאה ברורה, logout אוטומטי ב-401
- **זיהוי משתמש** — שם משתמש ו-badge "מנהל" בheader

---

## טכנולוגיות שנבחרו ומדוע

| טכנולוגיה | סיבה |
|---|---|
| **ASP.NET Core 8** | הפלטפורמה הקיימת בפרויקט |
| **EF Core + IQueryable** | סינון ב-SQL, לא בזיכרון — קריטי למיליוני רשומות |
| **SQLite** | בסיס נתונים אמיתי עם קובץ יחיד — אינדקסים פועלים בפועל, אין התקנה |
| **Angular 17 (NgModule)** | שומר עקביות עם הקוד הקיים |
| **ReactiveFormsModule** | ניהול טופס הסינון — ולידציה בקוד TypeScript, האזנה לשינויי תאריך בזמן אמת |

---

## אימות — JWT

המערכת משתמשת ב-JWT Bearer authentication.

### התחברות

שלח `POST /api/auth/login` עם:
```json
{ "userId": 1, "password": "user1" }
```

מחזיר `{ "token": "<jwt>" }`. הטוקן תקף ל-24 שעות.

### משתמשי demo

| userId | password | תפקיד |
|--------|----------|-------|
| 1 | user1 | משתמש רגיל — רואה רק בקשות שלו |
| 2 | user2 | משתמש רגיל — רואה רק בקשות שלו |
| 3 | admin | מנהל — רואה את כל הבקשות |

### איך זה עובד

- ה-token נחתם עם HMAC-SHA256 ומכיל `userId` ו-`isAdmin` כ-claims.
- כל בקשה ל-`/api/requests/*` דורשת `Authorization: Bearer <token>`.
- ה-Controller חולץ את זהות המשתמש מה-token — לא מ-headers שהקליינט שולח.
- בצד ה-Angular: `authInterceptor` מוסיף את ה-header אוטומטית לכל בקשה.
- תגובת 401 מה-API גורמת ל-logout אוטומטי ב-frontend.

### הנחות שבוצעו

- **SecretKey** — מוגדר ב-`appsettings.json` לצורך dev. בפרודקשן: Environment Variable / Azure Key Vault.
- **משתמשים** — hard-coded ב-`AuthController`. בפרודקשן: טבלת `Users` עם סיסמאות BCrypt.
- **SQLite** — מתאים לפיתוח ולהדגמה. בפרודקשן: החלפת שורה אחת ב-`DependencyInjection.cs` ל-`UseSqlServer`.
- **Login UI** — דף הכניסה מציג כפתורי בחירת משתמש במקום טופס username/password, כדי להקל על הבוחן לעבור בין משתמשים ולבדוק הרשאות. התשתית קיימת במלואה — JWT, interceptor, 401 handling. בפרודקשן: טופס אמיתי מול טבלת Users עם BCrypt.

---

## החלטה טכנית 1: Pagination ברמת ה-DB

עם מיליוני רשומות, pagination הוא הדבר הכי קריטי לביצועים — לפניו אפילו אינדקסים מושלמים לא יעזרו, כי עדיין מחזירים מיליון שורות ברשת.

הפתרון: `CountAsync` לפני החיתוך (כדי ש-`totalCount` ייצג את סך הרשומות התואמות), ואז `Skip/Take` כחלק מה-`IQueryable` — הכל מתורגם ל-SQL אחד שמחזיר בדיוק `pageSize` שורות:

```csharp
var totalCount = await query.CountAsync();
var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
```

```sql
-- מה שרץ בפועל:
SELECT * FROM Requests WHERE ...
ORDER BY CreatedAt DESC
OFFSET 0 ROWS FETCH NEXT 20 ROWS ONLY;  -- 20 שורות בלבד, לא מיליון
```

`pageSize` מוגבל ל-200 — מגן מפני `pageSize=999999` שיעקוף את כל ההגנה.

בקליינט: כל שינוי פילטר מאפס ל-`page=1`, הפקינציה מוסתרת כש-`totalCount=0`.

---

## החלטה טכנית 2: IQueryable pipeline במקום סינון in-memory

### החלופות

**חלופה א׳ — סינון in-memory (הגישה הקיימת לפני הפיצ'ר):**
```csharp
var all = await _db.Requests.ToListAsync(); // טוען 100,000 שורות לזיכרון
return all.Where(r => r.Status == status).Take(20);
```

**חלופה ב׳ — IQueryable pipeline (מה שנבחר):**
```csharp
var q = _db.Requests.AsQueryable();
if (status != null) q = q.Where(r => status.Contains(r.Status));
var total = await q.CountAsync();
var items = await q.Skip(...).Take(...).ToListAsync(); // מחזיר 20 שורות בלבד
```

### מדוע נבחרה חלופה ב׳

- **זיכרון** — in-memory טוען את כל הטבלה. עם 100,000 רשומות זה ~50MB לכל request.
- **TotalCount נכון** — `CountAsync()` לפני `Skip/Take` מחזיר את סך הרשומות התואמות.
- **ביצועים קבועים** — זמן תגובה לא גדל עם גידול הטבלה.

---

## החלטה טכנית 3: SQLite במקום InMemory

### הבעיה עם InMemory

`UseInMemoryDatabase` (הגישה המקורית) מתעלם לחלוטין מ-indexes. כל query סורק את כל הרשומות — אין תועלת להגדיר אינדקסים.

### מה שנבחר

מעבר ל-SQLite: קובץ `requests.db` שנוצר אוטומטית. הגדרת `EnsureCreated()` ב-`Program.cs` יוצרת את הטבלות **והאינדקסים** בפעם הראשונה.

### האינדקסים שהוגדרו ב-DbContext

```csharp
entity.HasIndex(r => r.Status);
entity.HasIndex(r => r.RequestType);
entity.HasIndex(r => r.CreatedAt);
entity.HasIndex(r => r.RequestNumber);
entity.HasIndex(r => r.AssignedToUserId);
entity.HasIndex(r => new { r.OwnerId, r.CreatedAt }); // composite — ownership + sort
```

ה-composite index על `(OwnerId, CreatedAt)` מייעל את הקייס הנפוץ ביותר: משתמש רגיל מסנן לפי ownership ואז ממיין לפי תאריך.

### מעבר לפרודקשן

שינוי שורה אחת בלבד ב-`DependencyInjection.cs`:
```csharp
// לפני (SQLite):
options.UseSqlite("Data Source=requests.db")

// אחרי (SQL Server):
options.UseSqlServer(connectionString)
```

כל שאר הקוד — Repository, Service, Controller — לא משתנה.

---

## הרשאות — Admin vs משתמש רגיל

הדרישה מחייבת אכיפת הרשאות **בצד השרת בלבד** — הקליינט לא יכול לעקוף אותן.

### איך זה עובד

ה-JWT token מכיל שני claims: `userId` ו-`isAdmin`. הקליינט לא שולח אותם כ-headers — הם נחתמים בתוך ה-token ב-login ואי אפשר לזייף אותם.

ה-Controller חולץ אותם:
```csharp
var userIdClaim = User.FindFirst("userId")?.Value;
var isAdminClaim = User.FindFirst("isAdmin")?.Value;
```

ה-Repository מיישם את הסינון ישירות ב-`IQueryable` לפני ביצוע השאילתה ב-DB:
```csharp
if (!isAdministrator)
    query = query.Where(r => r.OwnerId == userId || r.AssignedToUserId == userId);
```

כלומר משתמש רגיל **פיזית לא יכול לקבל** רשומות של אחרים — גם אם ישלח בקשה ישירה ל-API עם פילטרים מניפולטיביים.

### למה ב-Repository ולא ב-Service

אפשר היה לסנן ב-Service אחרי קבלת הנתונים — אבל אז ה-pagination יהיה לא נכון. אם יש 100 רשומות ב-DB ו-80 שייכות למשתמש אחר, סינון in-memory אחרי `Take(20)` יחזיר פחות מ-20 תוצאות. סינון ב-`IQueryable` מבטיח ש-`totalCount` ו-pagination מדויקים.

---

## מה לא הספקתי ואיך הייתי ממשיך

### לא הוספתי
- **בדיקות Integration** — הבדיקות הן unit tests ל-Service. בדיקות integration מול SQLite בזיכרון (EF Core Test Helpers) היו מוסיפות ביטחון שה-IQueryable pipeline מתורגם ל-SQL נכון.
- **Outbox Pattern** — תוכנן ב-design.md, לא מומש. הצעד הבא לארכיטקטורת Microservices אמינה.

### איך הייתי ממשיך
1. בדיקות integration עם `WebApplicationFactory` + SQLite
2. Outbox + background publisher ב-`RequestsService`
3. Docker + Azure Container Apps deployment

---

## Architecture – חלק ב

תכנון Microservices ותקשורת אמינה בין שירותים (Outbox Pattern) נמצאים ב:
```
architecture.md
```
כולל: חלוקה לשירותים, תרשים, תועלות, ותכנון Notification עם Transactional Outbox Pattern.

## החלטה טכנית 4: כפתור חיפוש במקום Live Filtering

### מה שנבחר

הטופס שולח בקשה לשרת רק בלחיצה על כפתור "חפש" — לא על כל שינוי בשדה.

### למה

המערכת מניחה מיליוני רשומות. כל קריאה לשרת מריצה SQL עם joins ואינדקסים — לא פעולה זולה. עם live filtering, מילוי טופס עם 3 פילטרים שונים מייצר 3+ קריאות לשרת. עם כפתור — קריאה אחת בלבד אחרי שהמשתמש סיים להגדיר את כל הפילטרים.

בנוסף, `switchMap` מבטל קריאה ישנה בצד הקליינט — אבל השרת כבר התחיל להריץ את ה-SQL. הביטול לא חוסך עומס מהשרת.

### חלופה — Live Filtering

אם חוויית המשתמש חשובה יותר מביצועים (למשל — מאגר נתונים קטן, או שהמשתמשים מצפים לתגובה מיידית), אפשר לעבור ל-live filtering:
- הוסף `valueChanges` עם `debounce(500ms)` ו-`distinctUntilChanged` על כל שדה
- הוסף `switchMap` ב-`app.ts` — מבטל קריאה קודמת כשיוצאת חדשה
- הסר את כפתור "חפש"

זה שינוי של ~15 שורות קוד. הבחירה תלויה בפרופיל המשתמשים ובגודל הנתונים.

---

## החלטה טכנית 5: JWT Authentication

### מה שנבנה

אימות מלא מקצה לקצה:

- **`POST /api/auth/login`** — מקבל `userId + password`, מחזיר JWT token חתום עם HMAC-SHA256
- **claims בתוך ה-token**: `userId`, `isAdmin` — השרת קורא מהם, לא מ-headers שהקליינט שולח
- **`[Authorize]`** על `RequestsController` — בקשה ללא token תקין מקבלת 401 אוטומטית
- **`authInterceptor`** ב-Angular — מוסיף `Authorization: Bearer <token>` לכל בקשה בשקט
- **401 handling** ב-`app.ts` — token שפג תוקף גורם ל-logout אוטומטי



### נקודות לשיפור לפרודקשן

**1. httpOnly Cookie במקום localStorage**

Token ב-`localStorage` חשוף ל-XSS — כל JavaScript זדוני בדפדפן יכול לגנוב אותו. הפתרון הנכון: שמירת ה-token ב-`httpOnly cookie` שהדפדפן שולח אוטומטית וJS אינו יכול לגשת אליו. מחיר: צריך לטפל ב-CSRF protection.

**2. Refresh Token**

כרגע token תקף ל-24 שעות — כשפג, המשתמש צריך להתחבר מחדש. בפרודקשן: access token קצר (15 דקות) + refresh token ארוך (7 ימים) שמתחדש בשקט ברקע.

**3. BCrypt לסיסמאות**

המשתמשים hard-coded עם סיסמאות בטקסט פתוח. בפרודקשן: טבלת `Users` עם `BCrypt.HashPassword` — גם אם ה-DB נגנב, הסיסמאות לא נחשפות.

**4. SecretKey מחוץ לקוד**

ה-`SecretKey` ב-`appsettings.json` נכנס ל-source control. בפרודקשן: Environment Variable או Azure Key Vault — הסוד לא נמצא בשום קובץ בריפו.

---

## החלטה טכנית 6: ערכי סינון hard-coded בקליינט

### המצב הנוכחי

ערכי `Status` ו-`RequestType` מוגדרים כ-enum בקוד C# ומשוכפלים בקליינט:

```typescript
readonly statusOptions = [
  { label: 'חדש',   value: RequestStatus.New },
  { label: 'בטיפול', value: RequestStatus.InProgress },
  ...
];
```

### למה זה מקובל כאן

`RequestStatus` ו-`RequestType` הם enum קבוע בקוד — הם לא מגיעים ממסד נתונים. שינוי שלהם תמיד ידרוש שינוי קוד ופריסה מחדש של השרת. אז הקליינט ממילא יתעדכן באותה פריסה — שתי נקודות שינוי אבל מסונכרנות.

### מתי היה נכון לעשות אחרת

אם הערכים היו מנוהלים דינמית במסד נתונים (למשל — מנהל מוסיף סטטוס חדש דרך UI ללא פריסה), אז הגישה הנכונה היא:

```
GET /api/requests/metadata
→ { statuses: [{value: 1, label: "חדש"}, ...], requestTypes: [...] }
```

הקליינט קורא את ה-endpoint הזה פעם אחת בהפעלה ומציג מה שמגיע מהשרת — דינמי לחלוטין, נקודת שינוי אחת.

---

`GET /api/requests/search`

| Parameter | Type | Default | Description |
|---|---|---|---|
| `requestNumber` | `string` | — | חיפוש חלקי (case-insensitive) |
| `status` | `int[]` | — | `1`=חדש, `2`=בטיפול, `3`=הושלם, `4`=בוטל |
| `requestType` | `int[]` | — | `1`=כללי, `2`=משפטי, `3`=תשלום, `4`=ערר |
| `createdFrom` | `DateTime` | — | `CreatedAt >= createdFrom` |
| `createdTo` | `DateTime` | — | `CreatedAt <= createdTo` |
| `sortBy` | `string` | `CreatedAt` | `Id`, `RequestNumber`, `Status`, `RequestType`, `CreatedAt`, `OwnerId` |
| `sortDirection` | `string` | `desc` | `asc` או `desc` |
| `page` | `int` | `1` | מינימום 1 |
| `pageSize` | `int` | `20` | מינימום 1, מקסימום 200 |

## Headers

| Header | תיאור |
|---|---|
| `Authorization` | חובה. `Bearer <jwt_token>` — מתקבל מ-`POST /api/auth/login` |

