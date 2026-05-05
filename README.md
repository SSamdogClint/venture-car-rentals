# 🚗 Venture Car Rentals

A web-based **Car Rental Management System** developed using:

* ASP.NET Core Razor Pages (.NET 8)
* Entity Framework Core (EF Core)
* SQLite Database
* C#

---

# 📌 Project Description

**Venture Car Rentals** is a system that allows users to:

* Register and log in securely
* Browse available cars
* Book rental vehicles
* Upload required documents
* Make payments
* Leave reviews

Administrators can:

* Manage cars (CRUD)
* Monitor bookings
* Manage maintenance logs
	* View system reports

---

# 🛠️ Technologies Used

* C# (.NET 8)
* ASP.NET Core Razor Pages
* Entity Framework Core
* SQLite
* Bootstrap 5 (Responsive UI)

---

# 📁 Project Structure

```text
VentureCarRentals/
│
├── Data/
│   ├── AppDbContext.cs
│   └── DbSeeder.cs
│
├── Models/
│   ├── User.cs
│   ├── Car.cs
│   ├── Booking.cs
│   ├── Payment.cs
│   ├── RentalAgreement.cs
│   ├── UserDocument.cs
│   ├── Review.cs
│   └── MaintenanceLog.cs
│
├── Pages/
│   ├── Index.cshtml
│   ├── Register.cshtml
│   ├── Login.cshtml
│   ├── Logout.cshtml
│   │
│   ├── Admin/
│   │   └── Dashboard.cshtml
│   │
│   ├── User/
│   │   └── Dashboard.cshtml
│   │
│   ├── Shared/
│   │   └── _Layout.cshtml
│
├── wwwroot/
│   ├── css/
│   ├── js/
│   └── images/
│
├── Migrations/
├── appsettings.json
├── Program.cs
└── README.md
```

---

# ⚙️ HOW TO SETUP

## 1. Clone the repository

```bash
git clone https://github.com/SSamdogClint/venture-car-rentals.git
cd venture-car-rentals
```

---

## 2. Open the project

Open using **Visual Studio 2022**

```text
Open → VentureCarRentals.csproj
```

---

## 3. Restore dependencies

```bash
dotnet restore
```

---

## 4. Create database

```bash
dotnet ef database update
```

✔ This will automatically create the SQLite database.

---

## 5. Run the application

```bash
dotnet run
```

or press:

```text
F5 (Run)
```

---

# 🔐 AUTHENTICATION SYSTEM

* User registration with password hashing (BCrypt)
* Login with session-based authentication
* Role-based redirection:

  * Admin → `/Admin/Dashboard`
  * User → `/User/Dashboard`
* Logout clears session and redirects to homepage

---

# 👤 DEFAULT ADMIN ACCOUNT

The system automatically creates an admin account if it does not exist:

```text
Email: admin@venture.com
Password: admin123
```

---

# 🔄 SYSTEM FEATURES (CURRENT STATUS)

### ✅ Completed

* User Registration & Login
* Password Hashing (BCrypt)
* Session Management
* Role-Based Access (Admin/User)
* Responsive UI (Mobile/Desktop)
* Dashboard (Admin & User Mockups)

### 🔄 In Progress

* Car Management (CRUD)
* Booking System (Transaction Module)
* Payment Integration
* Document Verification
* Reports Generation

---

# 🔐 SECURITY FEATURES

* Password hashing (BCrypt)
* Session-based authentication
* Role-based access control
* Input validation

---

# 🧪 DEVELOPMENT NOTES

* Hot Reload enabled for faster UI updates
* Database auto-created via EF Core migrations
* Admin account seeded automatically on startup

---

# 👥 TEAM

* Dedumo, Lyle Adrien — Project Manager
* Ferrer, Krist Dave — UI/UX Designer
* Capondag, Clint Eroll — Backend Developer
* Cuerda, Carlos Jose — Frontend Developer
* Loyola, Ian Francis — QA Tester
* Quillosa, Geian Francis — Product Owner

---

# 📌 PROJECT STATUS

This system is currently in:

```text
PHASE 11 — FEATURE IMPLEMENTATION
```

Completed:

* Project setup
* Database design
* Authentication system
* UI design

Next:

* CRUD operations
* Booking transactions
* Reports

---

# 🚀 FUTURE IMPROVEMENTS

* REST API integration
* Mobile app support
* Online payment gateway
* Advanced analytics dashboard

---

# 📄 LICENSE

This project is developed for academic purposes.
