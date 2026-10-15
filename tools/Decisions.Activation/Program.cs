using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Aetheric.Provisioning.Engine;
using Aetheric.Provisioning.Persistence;

// Runs beside the admin's encrypted credential store. Never prints credentials or tokens.
// Inspect is read-only; prepare creates only this office's groups and adds an existing user.
if (args.Length != 2 || args[0] is not ("inspect" or "prepare") || string.IsNullOrWhiteSpace(args[1]))
{
    Console.Error.WriteLine("Usage: Decisions.Activation inspect|prepare <existing-sso-username>");
    return 2;
}
const string realm = "int.aethericforge.ca";
const string organization = "administration-decisions";
var deploymentId = InstitutionDeploymentAttempt.Identity("https://github.com/aetheric-forge/adr-campus", "decisions", "production");
var store = new ManagedRootCredentialStore(
    Environment.GetEnvironmentVariable("RootCredentials__Directory") ?? "/data/root-credentials",
    Environment.GetEnvironmentVariable("RootCredentials__KeyDirectory") ?? "/data/root-key");
try
{
    var root = await store.TryReadAsync("keycloak", default)
        ?? throw new InvalidOperationException("Saved Keycloak credential is required.");
    var options = root.Keycloak ?? throw new InvalidOperationException("Saved Keycloak options are required.");
    using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
    {
        BaseAddress = new UriBuilder(options.Scheme, root.Host, root.Port, options.BasePath).Uri,
        Timeout = TimeSpan.FromSeconds(30)
    };
    using var tokenResponse = await http.PostAsync($"realms/{Uri.EscapeDataString(options.Realm)}/protocol/openid-connect/token",
        new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password", ["client_id"] = options.ClientId,
            ["username"] = root.Username!, ["password"] = root.Password
        }));
    RequireSuccess(tokenResponse);
    using var token = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync());
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.RootElement.GetProperty("access_token").GetString());
    var prefix = $"admin/realms/{Uri.EscapeDataString(realm)}/";
    var users = await ReadArray($"users?username={Uri.EscapeDataString(args[1])}&exact=true");
    if (users.Length != 1 || users[0].GetProperty("username").GetString() != args[1]
        || !users[0].GetProperty("enabled").GetBoolean())
        throw new InvalidOperationException("Select exactly one existing, enabled SSO username.");
    var userId = users[0].GetProperty("id").GetString()!;
    // Validate both names before creating either group or changing any memberships.
    var names = new[] { organization + "-members", organization + "-maintainers" };
    var groups = new List<(string Name, string? Id)>();
    foreach (var name in names) groups.Add((name, await FindOwnedGroup(name)));
    var memberships = await ReadArray($"users/{Uri.EscapeDataString(userId)}/groups?first=0&max=10000");
    if (memberships.Length >= 10000) throw new InvalidOperationException("Membership pagination limit reached.");
    var memberIds = memberships.Select(x => x.GetProperty("id").GetString()).ToHashSet();
    var result = new Dictionary<string, string>();
    foreach (var (name, priorId) in groups)
    {
        var id = priorId;
        if (args[0] == "prepare")
        {
            if (id is null)
            {
                using var created = await http.PostAsJsonAsync(prefix + "groups", new
                {
                    name,
                    attributes = new Dictionary<string, string[]>
                    {
                        ["aetheric.deployment-id"] = [deploymentId],
                        ["aetheric.organization-id"] = [organization]
                    }
                });
                // Concurrent creation is safe only when the group has our ownership attributes.
                if (created.StatusCode != HttpStatusCode.Conflict) RequireSuccess(created);
                id = await FindOwnedGroup(name) ?? throw new InvalidOperationException("Created group could not be verified.");
            }
            if (!memberIds.Contains(id))
            {
                using var added = await http.PutAsync(prefix + $"users/{Uri.EscapeDataString(userId)}/groups/{Uri.EscapeDataString(id)}", null);
                RequireSuccess(added);
            }
        }
        result[name.EndsWith("-maintainers") ? "MaintainerGroupId" : "MemberGroupId"] = id ?? "not-created";
    }
    if (args[0] == "prepare")
    {
        var verified = await ReadArray($"users/{Uri.EscapeDataString(userId)}/groups?first=0&max=10000");
        var ids = verified.Select(x => x.GetProperty("id").GetString()).ToHashSet();
        if (result.Values.Any(id => !ids.Contains(id))) throw new InvalidOperationException("Membership verification failed.");
    }
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        Mode = args[0], Realm = realm, OrganizationId = organization, DisplayName = "Administration Decisions",
        DeploymentId = deploymentId, InitialMaintainer = args[1], Groups = result,
        HostActivated = false
    }, new JsonSerializerOptions { WriteIndented = true }));
    return 0;

    async Task<JsonElement[]> ReadArray(string path)
    {
        using var response = await http.GetAsync(prefix + path);
        RequireSuccess(response);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.EnumerateArray().Select(x => x.Clone()).ToArray();
    }
    async Task<string?> FindOwnedGroup(string name)
    {
        var matches = (await ReadArray($"groups?search={Uri.EscapeDataString(name)}&exact=true&briefRepresentation=false&first=0&max=100"))
            .Where(x => x.GetProperty("name").GetString() == name).ToArray();
        if (matches.Length == 0) return null;
        if (matches.Length != 1) throw new InvalidOperationException("Ambiguous Decisions group name.");
        var group = matches[0];
        var id = group.GetProperty("id").GetString()!;
        // Read the complete representation; group search can omit attributes on some servers.
        using var response = await http.GetAsync(prefix + "groups/" + Uri.EscapeDataString(id));
        RequireSuccess(response);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var full = document.RootElement;
        if (full.GetProperty("path").GetString() != "/" + name
            || !full.TryGetProperty("attributes", out var attributes)
            || !OwnedAttribute(attributes, "aetheric.deployment-id", deploymentId)
            || !OwnedAttribute(attributes, "aetheric.organization-id", organization))
            throw new InvalidOperationException("Existing group is not owned by this Decisions office; refusing adoption.");
        return id;
    }
}
catch (Exception error) when (error is HttpRequestException or JsonException or InvalidOperationException or IOException)
{
    // Do not emit server bodies, root objects, URLs containing tokens, or exception chains.
    Console.Error.WriteLine(error is HttpRequestException ? "Identity request failed; check connectivity and permissions."
        : error is JsonException ? "Identity response had an unexpected format."
        : error is IOException ? "Could not read the encrypted credential store." : error.Message);
    return 1;
}

static bool OwnedAttribute(JsonElement attributes, string key, string expected) =>
    attributes.TryGetProperty(key, out var values) && values.ValueKind == JsonValueKind.Array
    && values.GetArrayLength() == 1 && values[0].GetString() == expected;

static void RequireSuccess(HttpResponseMessage response)
{
    if (!response.IsSuccessStatusCode)
        throw new InvalidOperationException($"Identity operation returned HTTP {(int)response.StatusCode}.");
}
