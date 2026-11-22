namespace RestaurantHandler.Utilities
{
    public static  class HelpersFunctions
    {
        /// <summary>
        /// Numéro de commande lisible : <c>BP_yyyyMMdd_NNN</c> où NNN est le rang de la
        /// commande dans la journée (UTC), ex. <c>BP_20260421_005</c> pour la 5e commande
        /// du 21 avril 2026. Le séquentiel est calculé par l'appelant ; l'unicité réelle
        /// est garantie par l'index unique sur <see cref="Models.Order.OrderNumber"/>
        /// (l'appelant réessaie en incrémentant le rang en cas de collision concurrente).
        /// </summary>
        public static string GenerateOrderNumber(DateTime dateUtc, int sequence)
        {
            return $"BP_{dateUtc:yyyyMMdd}_{sequence:D3}";
        }

        /// <summary>
        /// Code de présence à 6 chiffres (100000–999999) remis à l'élève. Un espace
        /// large + le verrouillage anti-bruteforce rend le devinage impraticable.
        /// </summary>
        public static int GenerateValidationCode()
        {
            return Random.Shared.Next(100000, 1000000);
        }
    }
}
