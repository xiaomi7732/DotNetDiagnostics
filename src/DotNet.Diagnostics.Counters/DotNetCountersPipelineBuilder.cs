namespace Microsoft.Extensions.DependencyInjection;

public class DotNetCountersPipelineBuilder
{
    private Action<IServiceCollection> _actions;
    public IServiceCollection Services { get; }
    public string SectionName { get; }

    public DotNetCountersPipelineBuilder(
        Action<IServiceCollection> actions,
        IServiceCollection services,
        string sectionName)
    {
        if (string.IsNullOrEmpty(sectionName))
        {
            throw new ArgumentException($"'{nameof(sectionName)}' cannot be null or empty.", nameof(sectionName));
        }
        _actions = actions;
        Services = services ?? throw new ArgumentNullException(nameof(services));
        SectionName = sectionName;
    }

    public DotNetCountersPipelineBuilder AppendAction(Action<IServiceCollection> action)
    {
        _actions += action;
        return this;
    }

    public void Register()
    {
        _actions.Invoke(Services);
    }
}