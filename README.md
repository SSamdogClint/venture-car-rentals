# Venture Car Rentals

Venture Car Rentals is a web-based car rental management system developed using ASP.NET Core Razor Pages, Entity Framework Core, SQLite, Bootstrap 5, and C#.

The system allows guests to browse vehicles, users to create bookings and upload renter requirements, and administrators to manage cars, bookings, payments, reports, and rental operations.

---

# Features

## Guest Features

Guests can:

* Browse available vehicles
* View vehicle details and reviews
* Search cars by borrow and return schedule
* Access the landing page without logging in
* Redirect to Sign Up / Sign In before booking

---

## User Features

Users can:

* Register and log in
* Browse available cars
* Filter vehicles by category, transmission, seats, and price
* Book vehicles
* Upload renter verification documents
* Manage profile information
* View booking history
* View payment records
* Access rental agreements
* Submit vehicle reviews
* Receive verification status updates

### Renter Verification

### Local Renters

Required documents:

* Driver’s License
* One secondary valid ID

Accepted secondary IDs:

* National ID
* Police Clearance
* NBI Clearance
* PhilHealth ID
* SSS ID
* UMID
* Voter’s ID
* Company ID

### Foreign Renters

Required documents:

* Passport
* International Driving Permit / License

The system validates:

* Minimum age requirement (18 years old)
* Required profile fields
* Document approval status
* Document expiration

---

## Admin Features

Admins can:

* Access the admin dashboard
* Manage cars
* Upload car images
* Manage bookings
* Approve or reject payments
* View transaction history
* Generate PDF reports
* Manage maintenance logs
* Verify user documents
* Monitor statistics and summaries

### Reports Available

* Booking Report
* Car Report
* Payment Details Report
* Transactions Report

Reports support:

* Today
* Weekly
* Monthly
* Overall

PDF reports are generated using QuestPDF.

---

# Technologies Used

* ASP.NET Core Razor Pages
* .NET 8
* C#
* Entity Framework Core
* SQLite
* Bootstrap 5
* Bootstrap Icons
* QuestPDF
* BCrypt.Net
* Git
* GitHub

---

# Project Structure

```text
VentureCarRentals/
│
├── Data/
├── Filters/
├── Helpers/
├── Migrations/
├── Models/
├── Pages/
│   ├── Admin/
│   ├── Guest/
│   ├── User/
│   └── Shared/
│
├── Properties/
├── wwwroot/
│   ├── css/
│   ├── js/
│   ├── images/
│   ├── uploads/
│   └── generated-agreements/
│
├── appsettings.json
├── Program.cs
├── README.md
└── .gitignore
```

---

# Setup Guide After Cloning

## 1. Clone the Repository

```powershell
git clone https://github.com/SSamdogClint/venture-car-rentals.git
```

Go inside the repository:

```powershell
cd venture-car-rentals
```

If needed, go inside the project folder:

```powershell
cd VentureCarRentals
```

---

## 2. Open the Project

Open using:

* Visual Studio 2022
* Visual Studio Code

Open either:

```text
VentureCarRentals.sln
```

or:

```text
VentureCarRentals.csproj
```

---

## 3. Restore Packages

```powershell
dotnet restore
```

---

## 4. Build the Project

```powershell
dotnet build
```

---

# Database Setup

The SQLite database file is NOT uploaded to GitHub.

The database is recreated locally using Entity Framework Core migrations.

---

## Create the Database

Run:

```powershell
dotnet ef database update
```

This command automatically:

* Creates the SQLite database file
* Creates all database tables
* Applies the committed migrations

---

## If dotnet ef is Not Recognized

Install the EF Core CLI tool:

```powershell
dotnet tool install --global dotnet-ef
```

If already installed:

```powershell
dotnet tool update --global dotnet-ef
```

Then reopen the terminal.

---

# Run the Application

Run using terminal:

```powershell
dotnet run
```

Or run using Visual Studio:

* Press F5
* Or click the Run button

The app will run on a localhost URL such as:

```text
http://localhost:5002
```

or:

```text
https://localhost:7173
```

---

# Common New PC Setup Commands

After cloning on another computer:

```powershell
dotnet restore
dotnet build
dotnet ef database update
dotnet run
```

---

# Authentication and Authorization

The system supports:

* Guest access
* User accounts
* Admin accounts
* Role-based page protection
* Session authentication

### Redirect Flow

* Guests → Guest Browse Cars
* Users → User Home
* Admins → Admin Dashboard

---

# Booking Availability Logic

The system prevents overlapping bookings using:

```csharp
borrowDateTime < booking.EndDate &&
returnDateTime > booking.StartDate
```

This prevents double-booking of the same vehicle.

---

# Payment and Booking Workflow

1. User selects borrow and return schedule
2. User books available vehicle
3. Booking becomes pending
4. Admin approves booking
5. User signs rental agreement
6. Payment is processed
7. Booking becomes active/completed

---

# Generated and Uploaded Files

## Uploaded Documents

Stored in:

```text
wwwroot/uploads/
```

## Generated Agreements / PDFs

Stored in:

```text
wwwroot/generated-agreements/
```

Only file paths are stored in the database.

---

# Important GitHub Notes

The following files/folders are ignored in GitHub:

```text
bin/
obj/
.vs/
*.db
*.db-shm
*.db-wal
wwwroot/uploads/
wwwroot/generated-agreements/
wwwroot/rental-agreements/
```

These files are generated locally.

---

# If Database Is Deleted

Recreate it using:

```powershell
dotnet ef database update
```

---

# If Build Errors Occur

Stop the application and run:

```powershell
dotnet clean
dotnet build
```

---

# Team Roles

* Dedumo, Lyle Adrien — Project Manager
* Capondag, Clint Eroll — Backend Developer
* Ferrer, Krist Dave — UI/UX Designer
* Cuerda, Carlos Jose — Frontend Developer
* Loyola, Ian Francis — Quality Assurance Tester
* Quillosa, Geian Francis — Product Owner

---

# Project Requirements Covered

This project includes:

* ASP.NET Core Web Application
* Database Integration
* Authentication and Authorization
* Role-Based Access
* CRUD Operations
* Booking Management
* Payment Processing
* PDF Report Generation
* File Upload Handling
* Dashboard Statistics
* Validation and Error Handling
* Responsive Bootstrap UI

---

# Developer Note

This project is developed for academic purposes as a web-based car rental management system using ASP.NET Core Razor Pages and SQLite.

The system demonstrates backend development, database integration, session authentication, role-based access control, report generation, and responsive user interface design.
