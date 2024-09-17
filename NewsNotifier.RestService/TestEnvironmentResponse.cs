namespace NewsNotifier.RestService
{
    public class TestEnvironmentResponse
    {
        public bool IsRunningInAzureContainerApps { get; set; }
        public string RunningEnvironment { get; set; }
        public string Environment { get; set; }
        public Dictionary<string, string> EnvironmentVariables { get; set; }
    }
}