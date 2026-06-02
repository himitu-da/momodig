public sealed class FluidSimulationSolver
{
    public void Step(FluidManager manager, float deltaTime)
    {
        if (manager == null)
        {
            return;
        }

        manager.StepSimulationCore(deltaTime);
    }
}
