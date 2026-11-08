namespace EventHorizon.Colony
{
    /// <summary>Stored resources, all in kilograms.</summary>
    public enum ResourceType
    {
        Oxygen,
        Water,
        Food,
    }

    /// <summary>Generates electricity, e.g. a solar panel. Called once per simulation tick.</summary>
    public interface IPowerProducer
    {
        /// <summary>Returns the average output in watts over this tick.</summary>
        float ProduceWatts(float simDeltaSeconds);
    }

    /// <summary>Draws electricity, e.g. an oxygen plant or grow lights.</summary>
    public interface IPowerConsumer
    {
        float DemandWatts { get; }
        /// <summary>
        /// Called every tick, after the grid has balanced supply and demand.
        /// <paramref name="satisfaction"/> is the fraction of <see cref="DemandWatts"/> delivered (0..1).
        /// Do the resource work for the tick here.
        /// </summary>
        void Supply(float satisfaction, float simDeltaSeconds);
    }
}
