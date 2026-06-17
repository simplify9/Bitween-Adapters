using SW.Serverless.Sdk;

namespace SW.InfolinkAdapters.Receivers.TestPartnerProps;

class Program
{
    static async Task Main(string[] args) => await Runner.Run(new Handler());
}
