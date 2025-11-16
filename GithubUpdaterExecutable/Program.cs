using Octokit;

class Program
{
    static async Task<int> Main(string[] args)
    {
        if (args.Length < 4)
        {
            Console.WriteLine("Usage: GithubUpdater <repoOwner> <repoName> <currentVersion> <searchPath>");
            return 1;
        }
        var repoOwner = args[0];
        var repoName = args[1];
        var currentVersion = new Version(args[2]);
        var searchPath = args[3];
        var client = new GitHubClient(new ProductHeaderValue("BknibbGithubUpdater"));
        var release = await client.Repository.Release.GetLatest(repoOwner, repoName);
        if (release == null)
        {
            Console.Error.WriteLine($"Failed to check for updates");
            return 2;
        }
        var latestVersion = new Version(release.TagName.TrimStart('v'));
        if (latestVersion > currentVersion)
        {
            Console.WriteLine($"Updating to " + latestVersion);
            foreach (var asset in release.Assets)
            {
                var downloadAssetTask = client.Connection.GetRawStream(new Uri(asset.BrowserDownloadUrl), null);
                downloadAssetTask.Wait();
                if (downloadAssetTask.Result.HttpResponse.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    Console.WriteLine($"Failed to download asset {asset.Name}, status code: {downloadAssetTask.Result.HttpResponse.StatusCode}");
                    continue;
                }
                var target = Directory.GetFiles(searchPath, asset.Name, SearchOption.AllDirectories).FirstOrDefault() ?? Path.Join(searchPath, asset.Name);
                using (var file = File.Create(target))
                {
                    downloadAssetTask.Result.Body.CopyTo(file);
                }
                Console.WriteLine($"Asset {asset.Name} updated");
            }
            Console.WriteLine($"Updated to " + latestVersion);
        }
        else
        {
            Console.WriteLine($"Up to date");
        }
        return 0;
    }
}