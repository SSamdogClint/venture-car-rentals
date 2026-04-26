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
├── Pages/                     # Razor Pages (UI + backend logic)
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
│   ├── Cars/                 # Car CRUD (to be implemented)
│   ├── Bookings/             # Booking module (to be implemented)
│   └── Reports/              # Reports (to be implemented)
│
├── wwwroot/                  # Static files
│   ├── css/
│   ├── js/
│   └── lib/
│
├── Migrations/               # EF Core migrations
│
├── Properties/
│   └── launchSettings.json
│
├── appsettings.json
├── Program.cs
├── VentureCarRentals.csproj
├── README.md
└── .gitignore

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

✔ User Registration & Login  
✔ Car Management (CRUD)  
✔ Booking System  
✔ Payment Processing  
✔ Document Verification  
✔ Maintenance Tracking  
✔ Reviews & Ratings  
✔ Reports Generation  

---

# 🔐 SECURITY FEATURES

- Password hashing (BCrypt)
- Role-based access control
- Input validation
- Restricted admin actions

---

# 👨‍💻 DEVELOPERS

- Clint Eroll Capondag

---

# 📌 NOTES

This system is developed for academic purposes and fulfills the requirements:

- CRUD Operations
- Transaction Module
- Reports Generation
- Error Handling & Validation
- Security Implementation

