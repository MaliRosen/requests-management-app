# Requests Search & Filter

Full-stack feature: חיפוש, סינון, מיון ועימוד ברמת ה-DB על ASP.NET Core 8 API עם Angular 17 frontend.

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

## טכנולוגיות שנבחרו ומדוע

| טכנולוגיה | סיבה |
|---|---|
| **ASP.NET Core 8** | הפלטפורמה הקיימת בפרויקט |
| **EF Core + IQueryable** | סינון ב-SQL, לא בזיכרון — קריטי למיליוני רשומות |
| **SQLite** | בסיס נתונים אמיתי עם קובץ יחיד — אינדקסים פועלים בפועל, אין התקנה |
| **Angular 17 (NgModule)** | שומר עקביות עם הקוד הקיים |
| **ReactiveFormsModule** | debounce על שדה הטקסט בלי קוד נוסף |

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

---

## החלטה טכנית 1: IQueryable pipeline במקום סינון in-memory

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

## החלטה טכנית 2: SQLite במקום InMemory

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

## החלטה טכנית 4: JWT Authentication

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

## החלטה טכנית 5: ערכי סינון hard-coded בקליינט

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

