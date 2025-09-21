# 🎟️ Online Ticket Booking System

An **event management and ticketing platform** built with **ASP.NET Core (C#)** backend and **React + Tailwind** frontend.  
It allows **organizers** to create and manage events, ticket types, discounts, and payments, while **customers** can browse events, apply discounts, and purchase tickets securely.

---

## 🚀 Features

### 👤 User & Authentication
- User registration & login.
- JWT-based authentication.
- Profile page with password update.

### 🎫 Event Management
- Organizers can:
  - Create, update, and publish events.
  - Assign categories (Music, Drama, Sport, Festival, etc.).
  - Manage ticket types with sales periods, pricing, and limits.

- Customers can:
  - Browse published events.
  - Filter/search by **title, city, date, and category**.
  - View event details, ticket availability, and discounts.

### 💸 Payments (Stripe Integration)
- Integrated with **Stripe Checkout** (sandbox).
- Supports multiple currencies (`LKR`, `USD`).
- Handles **minimum transaction amounts** (auto-adjusts with fees).
- Secure flow:
  1. Create payment session.
  2. Redirect to Stripe Checkout.
  3. Webhook confirmation updates order/payment status.

### 🔗 Webhooks
- Stripe webhook listener to confirm payment success/failure.
- Prevents fraudulent confirmations by validating provider response.
- Ensures orders are marked **Confirmed** only after successful webhook.

### 🎟️ Discounts
- Organizers can:
  - Create discount codes with:
    - Percentage or fixed value.
    - Start/end date validity.
    - Max usage limits.
    - Scope (event-wide or ticket-type specific).
  - Manage discounts via an **admin dashboard**.

- Customers can:
  - Enter promo code at checkout.
  - Discount applied dynamically before payment.

- Backend automatically increments `UsedCount` when a discount is redeemed.

### 📧 Email Notifications
- Uses **Mailpit** (local email testing) for development.
- Sends styled HTML emails (modern, responsive templates).
- Example: Notify users when a new discount is available for an event.

### 📂 Categories
- Categories are **seeded** into the database (e.g., Music, Drama, Sport, Festival, Other).
- Events can optionally belong to one or more categories.
- Customers can filter events by category.

---

## 🛠️ Tech Stack

### Backend
- **ASP.NET Core 7 (C#)**
- **Entity Framework Core** (MySQL / MariaDB)
- **Stripe .NET SDK**
- **Background Queues** for async email sending
- **MailKit / Mailpit** for local email testing

### Frontend
- **React (Vite)**
- **Tailwind CSS**
- **React Hook Form** for forms
- **React Hot Toast** for notifications
- **Heroicons** for UI icons

### Database
- MySQL with EF Core Migrations
- Tables:
  - `Users`
  - `OrganizerProfiles`
  - `Events`
  - `TicketTypes`
  - `Orders`
  - `Payments`
  - `Discounts`
  - `Categories`
  - `EventCategories`

---

## ⚙️ Setup Instructions

### 1. Clone the repository
```bash
git clone git@github.com:prabath1998/event-ticketing-web-backend.git
cd event-ticketing-system
```

### 2. Configure appsettings.Development.json
```bash
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=EventTicketing;User=root;Password=yourpassword;"
  },
  "Stripe": {
    "SecretKey": "sk_test_xxx",
    "PublishableKey": "pk_test_xxx",
    "WebhookSecret": "whsec_xxx"
  },
  "Mail": {
    "Host": "localhost",
    "Port": 1025,
    "User": "",
    "Password": "",
    "From": "noreply@eventhub.test"
  },
  "FrontendOrigin": "http://localhost:3000"
}
```

### 3. Run migrations
```bash
dotnet ef database update
```

### 4. Start 
```bash
dotnet run
```
