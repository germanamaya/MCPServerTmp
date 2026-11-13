# Publish MCPServer to Azure App Service

The GitHub Actions workflow deploys the `src/MCPServer` .NET 10 application to the existing Windows App Service `mcpservertmp`. It runs only when manually started; it does not deploy on push.

For the detailed Spanish walkthrough covering Entra configuration, project managed identity app-role assignment, deployment, Foundry setup, and the token-cache issue resolved in this environment, see [Manual: App Service and Microsoft Foundry](./MANUAL-APP-SERVICE-FOUNDRY.md).

## Configure the publish profile

1. In the Azure portal, open App Service `mcpservertmp` and go to **Configuration → General settings**.
2. Enable **SCM Basic Auth Publishing Credentials** and save the configuration. The publish-profile deployment method requires this setting. Do not enable FTP basic authentication for this workflow.
3. Return to the Overview page and select **Download publish profile**.
4. In the GitHub repository, open **Settings → Secrets and variables → Actions → New repository secret**.
5. Create the secret `AZUREAPPSERVICE_PUBLISHPROFILE` and paste the complete contents of the downloaded publish-profile file as its value. Do not commit the profile file or paste it into workflow logs or issues.

The publish profile is a privileged deployment credential. Limit who can read repository secrets and run Actions, and regenerate the profile if it is exposed.

## Run a deployment

1. In GitHub, open **Actions → Deploy MCPServer to App Service**.
2. Select **Run workflow**, choose the branch to deploy, and start it.
3. The workflow restores and tests `MCPServer.sln`, publishes only `src/MCPServer/MCPServer.csproj` in Release configuration, and deploys the package to `mcpservertmp`.
4. After a successful run, verify that `https://<app-service-host>/health/live` returns `200` and `{"status":"ok"}`. The MCP endpoint is `https://<app-service-host>/mcp`; call it with an MCP client using Streamable HTTP and a valid bearer token. Opening the site root or `/mcp` in a browser without a token returns `401` by design.

The workflow does not create Azure resources or change App Service settings, Entra app registrations, app roles, or role assignments. Configure runtime authentication settings separately in App Service before expecting `/health/ready` or authenticated MCP calls to succeed. The local `ASPNETCORE_URLS=http://localhost:5000` value is not needed in App Service.
