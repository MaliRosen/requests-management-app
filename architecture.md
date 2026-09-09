# חלק ב – Architecture & תכנון

---

## חלוקה ל-Microservices

### תרשים

```
                        [ Angular Frontend ]
                                 ↓
                          [ API Gateway ]
                    (ניתוב, אימות JWT, rate limiting)
                                 ↓
        ┌────────────┬───────────┴───────────┬────────────────┐
        ↓            ↓                       ↓                ↓
 [ Identity    [ Requests      [ Customer        [ Notification
   Service ]     Service ]       Service ]         Service ]
   התחברות,      יצירה,          פרטי לקוח        מייל / SMS
   משתמשים       חיפוש,
                 סטטוסים
                     ↓
              [ Message Broker ]
          (Azure Service Bus / RabbitMQ)
                     ↓
            [ Notification Service ]
```

---

### השירותים — מה כל אחד עושה

**Identity Service**
אחראי על התחברות וניהול משתמשים. כיום הלוגיקה hard-coded ב-`AuthController` — במעבר ל-Microservices היא עוברת לשירות נפרד עם מסד נתונים משלו.

**Requests Service**
ליבת המערכת. יצירת בקשות, עדכון סטטוס, חיפוש וסינון. כל הקוד הקיים (`RequestService`, `RequestRepository`, `RequestsDbContext`) עובר לשירות זה.

**Customer Service**
כיום `CustomerId` הוא מספר יתום בלי טבלה. שירות זה יחזיק את פרטי הלקוחות ויחזיר אותם לצורך הצגה.

**Notification Service**
שולח מייל / SMS כשבקשה נוצרת או משנה סטטוס. מאזין ל-Message Broker בלבד — לא מקבל קריאות HTTP ישירות.

**API Gateway**
נקודת כניסה אחת לכל הקליינטים. מנתב בקשות לשירות המתאים, מאמת JWT, ומגביל קצב בקשות.

---

### תועלות

**Scale עצמאי** — Requests Service הוא ה-bottleneck (100,000+ רשומות, חיפוש כבד). אפשר להוסיף instances רק לו מבלי לגעת בשאר השירותים.

**Fault isolation** — אם Notification Service קורס, הבקשות ממשיכות להיווצר ללא הפרעה. כשל בשירות אחד לא מפיל את המערכת כולה.

**Deploy עצמאי** — כל שירות נפרס בנפרד. שינוי בלוגיקת החיפוש לא מצריך פריסה מחדש של שירות ההתראות.

**בעלות צוות** — כל צוות אחראי על שירות אחד. אין תיאום על אותו קוד.

---

## תקשורת אמינה בין שירותים — תרחיש Notification

## הבעיה

קריאת HTTP ישירה לשירות ההתראות אחרי שמירת הבקשה אינה מספיקה. אם השרת קרס בין שתי הפעולות — הבקשה נשמרה אך אין שום רשומה שמתעדת שהתראה צריכה להישלח. היא אובדת לתמיד.

## הפתרון — Transactional Outbox Pattern

כאשר בקשה נוצרת או משנה סטטוס, כותבים שתי רשומות באותה טרנזקציה:

```
BEGIN TRANSACTION
  INSERT requests       (הבקשה עצמה)
  INSERT outbox_events  (רשומה עם payload להתראה, status = PENDING)
COMMIT
```

שתיהן קיימות יחד או לא קיימות בכלל. אי אפשר לשמור בקשה מבלי שהמערכת זוכרת שצריך לשלוח התראה.

תהליך רקע (BackgroundService) קורא כל כמה שניות את הרשומות עם status = PENDING ושולח אותן ל-Message Broker. לאחר שליחה מוצלחת מעדכן status = DONE.

שירות ההתראות מאזין ל-Broker. כשמגיעה הודעה — בודק שלא טיפל בה בעבר, שולח מייל / SMS, ומאשר שהטיפול הסתיים.

## תרשים

```
Request Service
  BEGIN TRANSACTION
    INSERT requests
    INSERT outbox_events (PENDING)
  COMMIT
       ↓
  BackgroundService (כל כמה שניות)
  קורא PENDING → שולח → מעדכן DONE
       ↓
  Azure Service Bus / RabbitMQ
  (מחזיק הודעות עד שמישהו לוקח)
       ↓
  Notification Service
  בדוק כפילות → שלח מייל → ACK
```

## מה הפתרון מבטיח

| תרחיש | תוצאה |
|--------|-------|
| שירות ההתראות מושבת שעה | ההתראה נשלחת כשחוזר לחיים |
| השרת קרס אחרי COMMIT | הרשומה שרדה, ההתראה תישלח |
| ההודעה הגיעה פעמיים | מייל אחד בלבד — בדיקת כפילות מונעת שליחה כפולה |

## יישום ב-.NET

בפרויקט .NET הייתי משתמש ב-**MassTransit** — ספרייה שמממשת את ה-Outbox Pattern במלואו כולל קישור ל-Azure Service Bus, retry אוטומטי, וזיהוי כפילויות, ללא צורך לכתוב את הלוגיקה לבד.

זהו design pattern מתועד ומומלץ על ידי Microsoft ב-Azure Architecture Center.
