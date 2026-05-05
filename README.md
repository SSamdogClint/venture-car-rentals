# Venture Car Rentals

Venture Car Rentals is a web-based car rental management system developed using ASP.NET Core Razor Pages, Entity Framework Core, SQLite, Bootstrap, and C#.

The system allows users to browse cars, check car availability by selected borrow and return date/time, complete renter requirements, upload documents, and create bookings. Admin users can manage cars, upload car images, update car status, monitor availability, and view dashboard summaries.

---

## Project Description

Venture Car Rentals is designed to manage car rental transactions between renters and administrators.

### User Side

Users can:

- Register and log in
- Browse popular cars
- Search available cars by borrow and return date/time
- Complete renter profile information
- Upload required rental documents
- Book available cars

### Admin Side

Admins can:

- View dashboard statistics
- Manage cars
- Add new cars
- Upload car images
- Update car status
- Delete cars without booking records
- Monitor car availability

---

## Technologies Used

- C#
- ASP.NET Core Razor Pages
- .NET 8
- Entity Framework Core
- SQLite
- Bootstrap 5
- Bootstrap Icons
- Git
- GitHub

---

## Project Structure

VentureCarRentals/

├── Data/
│   └── AppDbContext.cs
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
│   ├── Admin/
│   │   ├── Dashboard.cshtml
│   │   ├── Dashboard.cshtml.cs
│   │   └── Cars/
│   │       ├── CarPage.cshtml
│   │       └── CarPage.cshtml.cs
│   │
│   ├── User/
│   │   ├── Home.cshtml
│   │   ├── Home.cshtml.cs
│   │   ├── _UserSidebar.cshtml
│   │   ├── Cars/
│   │   │   ├── BrowseCars.cshtml
│   │   │   └── BrowseCars.cshtml.cs
│   │   ├── Bookings/
│   │   │   ├── Create.cshtml
│   │   │   └── Create.cshtml.cs
│   │   └── Documents/
│   │       ├── CompleteRequirements.cshtml
│   │       └── CompleteRequirements.cshtml.cs
│   │
│   ├── Shared/
│   │   ├── _Layout.cshtml
│   │   ├── _AdminSidebar.cshtml
│   │   └── _ValidationScriptsPartial.cshtml
│   │
│   ├── Login.cshtml
│   ├── Login.cshtml.cs
│   ├── Register.cshtml
│   ├── Register.cshtml.cs
│   ├── Logout.cshtml
│   └── Logout.cshtml.cs
│
├── wwwroot/
│   ├── css/
│   │   └── site.css
│   ├── js/
│   │   └── site.js
│   ├── images/
│   │   ├── logo.png
│   │   └── cars/
│   └── uploads/
│       └── documents/
│
├── Migrations/
│
├── Properties/
│   └── launchSettings.json
│
├── appsettings.json
├── Program.cs
├── VentureCarRentals.csproj
├── VentureCarRentals.sln
├── README.md
└── .gitignore

---

## Setup Guide After Cloning

### 1. Clone the Repository

git clone https://github.com/SSamdogClint/venture-car-rentals.git

Go inside the repository folder:

cd venture-car-rentals

If the repository has a solution folder and project folder, go inside the project folder before running EF commands:

cd VentureCarRentals

---

### 2. Open the Project

Open the project using Visual Studio 2022.

You may open either:

VentureCarRentals.sln

or

VentureCarRentals.csproj

---

### 3. Restore Packages

Run:

dotnet restore

---

## Database Setup

The SQLite database file is not included in GitHub. It is generated locally.

### Option A: If the Migrations folder already exists

This is the normal setup after cloning.

Run:

dotnet ef database update

This will create the local SQLite database and apply the existing migrations.

---

### Option B: If there is no Migrations folder yet

Use this only if the project has no migration files yet.

First, make sure the project builds successfully:

dotnet build

Then create the first migration:

dotnet ef migrations add InitialCreate

Then create/update the database:

dotnet ef database update

---

### Option C: If you added new model fields

Use this when you changed a model, such as adding new fields to User.cs, Car.cs, Booking.cs, etc.

First, build the project:

dotnet build

Then create a new migration with a descriptive name:

dotnet ef migrations add AddUserProfileFields

Then update the database:

dotnet ef database update

---

### If dotnet ef is not found

Install the EF Core CLI tool:

dotnet tool install --global dotnet-ef

If it is already installed, update it:

dotnet tool update --global dotnet-ef

Then close and reopen the terminal.

Check if it works:

dotnet ef

---

## Run the Application

Run using terminal:

dotnet run

Or run using Visual Studio:

Press F5 or click the green Run button.

The app will run on a local address such as:

https://localhost:7173

---

## Common New PC Setup Commands

After cloning on a new computer, run:

dotnet restore
dotnet build
dotnet ef database update
dotnet run

If there is no Migrations folder yet, run:

dotnet restore
dotnet build
dotnet ef migrations add InitialCreate
dotnet ef database update
dotnet run

---

## Login Flow

The system supports user and admin login.

After login:

- Admin users are redirected to the admin dashboard
- Regular users are redirected to the user home page

---

## User Features

### User Home

Users can select:

- Borrow date
- Borrow time
- Return date
- Return time

After clicking Find Available Cars, the system displays cars that are available for the selected schedule.

### Browse Cars

If the user opens Browse Cars directly from the sidebar without selecting a date/time, the page displays popular available cars.

If the user searches using a borrow and return schedule, the system only shows cars that:

- Have status available
- Do not have overlapping bookings during the selected date and time

### Booking

When the user clicks Book, the system checks if the user has completed their renter profile and uploaded the required documents.

If the renter profile or documents are not complete, the user is redirected to the Complete Requirements page.

### Renter Requirements

First-time local renters must complete:

- Middle name
- Phone number
- Address
- Birthday
- Driver’s license
- Secondary ID

Accepted secondary IDs may include:

- National ID
- Police Clearance
- NBI Clearance
- PhilHealth ID
- SSS ID
- UMID
- Voter’s ID
- Company ID

Foreign renters must upload:

- Passport
- International Driving Permit or License

---

## Admin Features

### Admin Dashboard

The admin dashboard displays:

- Income summary
- Maintenance summary
- Booking status summary
- Live car status
- Booking summary

### Car Management

Admins can:

- Add cars
- Upload car images
- View car details
- Update car status
- Delete cars without booking records

Car statuses include:

- available
- booked
- maintenance
- inactive

---

## Booking Availability Logic

A car will not appear as available if it has an existing booking that overlaps with the selected borrow and return date/time.

Overlap checking uses this logic:

borrowDateTime < booking.EndDate && returnDateTime > booking.StartDate

This prevents double-booking of the same car.

---

## Uploaded Files

Car images are saved in:

wwwroot/images/cars/

User documents are saved in:

wwwroot/uploads/documents/

Only the file path is stored in the database.

---

## Important Notes

The SQLite database file is not included in GitHub.

The following files and folders should be ignored:

*.db
*.db-shm
*.db-wal
bin/
obj/
.vs/
*.user

If the database gets deleted locally, recreate it using:

dotnet ef database update

If the build shows old Razor errors, stop the app, delete bin and obj, then rebuild:

dotnet clean
dotnet build

---

## Team Roles

- Dedumo, Lyle Adrien — Project Manager
- Capondag, Clint Eroll — Backend Developer
- Ferrer, Krist Dave — UI/UX Designer
- Cuerda, Carlos Jose — Frontend Developer
- Loyola, Ian Francis — Quality Assurance Tester
- Quillosa, Geian Francis — Product Owner

---

## Project Requirements Covered

This system includes:

- C# .NET Application
- Database Integration
- User-Friendly Interface
- Master File Management
- Transaction Module
- Reports / Dashboard Summary
- Error Handling and Validation
- Basic Security Implementation
- Role-Based Access
- File Upload Handling

---

## Developer Note

This project is developed for academic purposes as a car rental management system using ASP.NET Core Razor Pages and SQLite.
