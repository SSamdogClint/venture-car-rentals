# 🚗 Venture Car Rentals

A web-based **Car Rental Management System** developed using:

- ASP.NET Core Razor Pages (.NET 8)
- Entity Framework Core (EF Core)
- SQLite Database
- C#

---

# 📌 Project Description

**Venture Car Rentals** is a system that allows users to:

- Register and log in
- Browse available cars
- Book vehicles
- Upload required documents
- Make payments
- Leave reviews

Admins can:

- Manage cars (CRUD)
- Monitor bookings
- Approve documents
- Manage maintenance logs

---

# 🛠️ Technologies Used

- C# (.NET 8)
- ASP.NET Core Razor Pages
- Entity Framework Core
- SQLite
- Bootstrap (UI)

---

# 📁 Project Structure

```text
VentureCarRentals/
│
├── Data/                      # Database context
│   └── AppDbContext.cs
│
├── Models/                    # Database models
│   ├── User.cs
│   ├── Car.cs
│   ├── Booking.cs
│   ├── Payment.cs
│   ├── RentalAgreement.cs
│   ├── UserDocument.cs
│   ├── Review.cs
│   └── MaintenanceLog.cs
│
├── Pages/                     # Razor Pages
│   ├── Index.cshtml
│   ├── Index.cshtml.cs
│   ├── Privacy.cshtml
│   ├── Privacy.cshtml.cs
│   │
│   ├── Shared/
│   │   ├── _Layout.cshtml
│   │   ├── _ValidationScriptsPartial.cshtml
│   │   └── _ViewImports.cshtml
│   │
│   ├── Cars/                  # Car CRUD
│   ├── Bookings/              # Booking module
│   └── Reports/               # Reports
│
├── wwwroot/                   # Static files
│   ├── css/
│   ├── js/
│   └── lib/
│
├── Migrations/                # EF Core migrations
├── Properties/
│   └── launchSettings.json
│
├── appsettings.json
├── Program.cs
├── VentureCarRentals.csproj
├── README.md
└── .gitignore
```

---

# ⚙️ HOW TO SETUP (AFTER CLONING)

## 1. Clone the repository

```bash
git clone https://github.com/SSamdogClint/venture-car-rentals.git
cd venture-car-rentals
```

---

## 2. Open the project

Open using **Visual Studio 2022**

Open → VentureCarRentals.csproj

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

This will automatically create the local SQLite database.

---

## 5. Run the application

```bash
dotnet run
```

OR press:

F5 (Run button in Visual Studio)

---

# ⚠️ IMPORTANT NOTES

- The database file is not included in the repository
- It will be generated automatically after running migrations
- Ensure `.gitignore` is properly configured

---

# 🔄 SYSTEM FEATURES

- User Registration & Login — To be implemented
- Car Management (CRUD) — To be implemented
- Booking System — To be implemented
- Payment Processing — To be implemented
- Document Verification — To be implemented
- Maintenance Tracking — To be implemented
- Reviews & Ratings — To be implemented
- Reports Generation — To be implemented

---

# 🔐 SECURITY FEATURES

- Password hashing (BCrypt)
- Role-based access control
- Input validation
- Restricted admin actions

---

# 👥 OUR TEAM
- Dedumo, Lyle Adrien	  - Project Manager
- Ferrer, Krist Dave	  - UI/UX Designer
- Capondag, Clint Eroll   - Backend Developer
- Cuerda, Carlos Jose	  - Frontend Developer
- Loyola, Ian Francis	  - Quality Assurance Tester
- Quillosa, Geian Francis - Product Owner

---

# 📌 NOTES

This system is developed for academic purposes and fulfills the requirements:

- CRUD Operations
- Transaction Module
- Reports Generation
- Error Handling & Validation
- Security Implementation

