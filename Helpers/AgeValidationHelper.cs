using System;

namespace VentureCarRentals.Helpers
{
    public static class AgeValidationHelper
    {
        /*
            IMPORTANT FEATURE:
            This checks if a renter is at least 18 years old.

            Why this is needed:
            - A car rental agreement is a legal contract.
            - The renter should be 18 years old or above before booking,
              verification, agreement signing, and payment completion.
        */
        public static bool IsAtLeast18(DateTime? birthday)
        {
            // If birthday is missing, the user is not allowed to continue.
            if (birthday == null)
            {
                return false;
            }

            var today = DateTime.Today;

            // Calculate age based on year difference.
            var age = today.Year - birthday.Value.Year;

            // Subtract 1 if the birthday has not happened yet this year.
            if (birthday.Value.Date > today.AddYears(-age))
            {
                age--;
            }

            return age >= 18;
        }

        /*
            This returns the user's actual age.
            Useful if you want to display age in Profile or Admin side later.
        */
        public static int? GetAge(DateTime? birthday)
        {
            if (birthday == null)
            {
                return null;
            }

            var today = DateTime.Today;
            var age = today.Year - birthday.Value.Year;

            if (birthday.Value.Date > today.AddYears(-age))
            {
                age--;
            }

            return age;
        }

        /*
            This gives one standard error message for all pages.
            Use this in Register, Profile, CompleteRequirements, and PaymentMethod.
        */
        public static string UnderAgeMessage =>
            "You must be at least 18 years old to rent a vehicle.";
    }
}