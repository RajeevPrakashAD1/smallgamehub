namespace TrailTrap
{
    /// <summary>
    /// Global dev-cheat switches. Plain static data so sim code can read them with zero
    /// wiring; DevPanel is the only writer. Release builds never show the panel, so these
    /// stay at their defaults there.
    /// </summary>
    public static class DevFlags
    {
        public static bool NoDeath;
    }
}
