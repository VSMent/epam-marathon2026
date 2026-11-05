## ✅📌 Домашнє завдання до воркшопу по Backend (.NET) #5

🎯 Груповий pre-order: функціонал для Owner
Оберіть рівень складності. Можна виконати кілька.

🟢 1й рівень
GET список сесій учасників групи: страви, суми по кожному та загальна сума.

🔵 2й рівень

GET guestSessionId owner session.
POST підтвердження консолідованого pre-order (лише submitted сесії, об'єднання страв по dishId, зміна статусів).

🟣 3й рівень
DELETE guest session із групи, у відповіді оновлений список і сума.

📎 Endpoint-и, приклади request/response та умови - у файлі до посту





## 🟩📌 Домашнє завдання по Frontend ) #6

🎯 Екран Owner групового pre-order
Owner бачить, що обрав кожен гість, і відправляє в ресторан одне спільне замовлення.

🚀 Як почати:

Розпакуйте архів і виконайте команди:
cd frontend/restaurant-frontend-app
npm ci
npm start
Відкрийте: http://localhost:4200/reservations/demo-reservation/group-order?demo
?demo - це мок-логін. Акаунт і бекенд не потрібні.
✅ Почніть з practice-frontend.md (~15 хв), далі homework-frontend.md.

📊 Рівні складності:

✅🟢 1й рівень - верстка екрана за макетом на статичних даних, без запитів.
✅🔵 2й рівень - повна логіка на мок-бекенді: дані з API, стани, спільне замовлення, Submit.
✅🟣 3й рівень - інтеграція зі справжнім API та синхронізація кожні 15 с.
✅🎁 Бонус - Remove Guest з діалогом підтвердження та оновленням замовлення.

Рівні 1 і 2 повністю на моках. Справжній бекенд потрібен тільки на 3-му.
Можна зупинитись на будь-якому рівні, головне - зробити його до кінця.

✅ Тести:
Тести до кожного рівня вже є в папці homework-tests/.

Скопіюйте тести рівня в src/app.
Напишіть код.
Запускайте npm run test:hw, поки всі тести не стануть зеленими.

🎨 Дизайн: папка homework-design/ та Figma (сторінка FOR DEVELOPERS, секція Owner Flow): Figma (https://www.figma.com/design/P9rkBNLJZ3WabXSQX9b5DO/EPAM-IT-Marathon-6.0?node-id=62645-20592)




## 🟩📌 Домашнє завдання по Manual QA #7

🎯 Аналіз вимог і тестування User Story
Ваше завдання: проаналізувати вимоги, покрити їх тест кейсами та протестувати функціонал.

Що потрібно зробити:

1. Проаналізуйте вимоги, знайдіть недоліки та опишіть їх:
User story: посилання
 (https://drive.google.com/file/d/13MZpQFxvRLquSdIPF7-wPU95x8gAVElv/view?usp=sharing)Figma design: figma (https://www.figma.com/design/P9rkBNLJZ3WabXSQX9b5DO/EPAM-IT-Marathon-6.0?node-id=111683-86716)
2. Покрийте User Story тест кейсами за шаблоном (https://docs.google.com/spreadsheets/d/1nOY-mdac7DvBux4VyclO6BOyY456S51a7pEfb0LFWUI/edit?pli=1&gid=0#gid=0)
3. Протестуйте User Story: керуючись скріншотом (https://drive.google.com/file/d/1qWITmyQg35sTWr9MLnzWQs1DJ6wpp_OA/view?usp=drive_link) з реалізованої частини застосунку 
4. Створіть дефект репорти за шаблоном (https://docs.google.com/spreadsheets/d/1nOY-mdac7DvBux4VyclO6BOyY456S51a7pEfb0LFWUI/edit?pli=1&gid=1194140512#gid=1194140512)





## 🟩📌 Домашнє завдання по Test Automation #8

🎯 Автоматизація end-to-end сценаріїв
Оберіть рівень, який вам підходить. Можна виконати кілька. Напрям залежить від того, що ви розгорнули в попередніх ДЗ. Деталі - у файлі до посту.

🟢 1й рівень
🔵 2й рівень
🟣 3й рівень

⭐️ Із зірочкою: звіти в Report Portal і пайплайн у хмарі.

📌 Здача: посилання на публічний git-репозиторій з тестами, пайплайном і README.

📎 Детальний опис - у файлі до посту






## 🟩FROM HOMEWORK 1
US1: As a booking organizer, I want to see identical items aggregated across all guests' pre-orders on a single summary screen, So that I can replace individual portions with larger sharing options or bulk sizes to save money.
    AC1: Given multiple guests have selected identical individual items (e.g. three 300 ml cups of tea), When the organizer views the group order summary screen, Then the system displays an upsell/bulk suggestion (e.g. "Combine into 1L teapot") along with the potential cost difference.
    AC2: Given a bulk suggestion is shown, When the organizer confirms the replacement, Then the summary updates the consolidated items and recalculates the total order price automatically.
    AC3:Given guests have selected distinct items or items without bulk equivalents, When the organizer opens the summary screen, Then no optimization hints are shown, and items remain grouped as they were.

US2: As a booking organizer, I want to filter and highlight alcoholic beverages within the combined group order, So that I can plan transportation logistics (booking taxis or assigning designated drivers) ahead of time.
    AC1: Given one or more guests have added alcoholic drinks to their pre-order, When the organizer views the group pre-order screen, Then an alcohol indicator icon is displayed next to each relevant guest and item.
    AC2: Given the group pre-order contains mixed items (alcoholic items included), When the organizer toggles the "Show Alcohol Only" filter, Then the screen filters the view to show only alcoholic drinks grouped by guest.
    AC3: Given the group order includes alcohol items, When the organizer clicks the "Plan Logistics" prompt on the summary screen, Then the system presents taxi/ride-sharing partner options with destination pre-filled as the restaurant address.

US3: As a booking organizer, I want to view a consolidated summary of all guests' pre-orders with per-person breakdowns and the total bill, So that I can verify that everyone has placed their order and check the estimated budget before confirming with the restaurant.
    AC1: Given guests have submitted or drafted their selections, When the organizer opens the group order dashboard, Then each guest's name is listed alongside their selected dishes, personal subtotals.
    AC2: Given all invited guests have submitted their pre-orders, When the organizer reviews the summary panel, Then the screen displays the total estimated cost, including taxes and service charges. 
    AC3: Given at least one invited guest has not chosen any dishes yet, When the organizer views the list, Then an empty status warning is visible next to their name with an option to send a reminder notification.
    AC4: Given an invited guest has not chosen any dishes yet and displays an empty status warning, When the organizer clicks the "Send Reminder" button next to that guest, Then the system dispatches a notification to the guest and displays a confirmation toast message, updating the button to sent.