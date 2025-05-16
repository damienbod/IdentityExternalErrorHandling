var builder = DistributedApplication.CreateBuilder(args);

var userName = builder.AddParameter("userName");
var passwordKeycloak = builder.AddParameter("passwordKeycloak", secret: true);
var keycloak = builder.AddKeycloakContainer("keycloak", userName: userName, password: passwordKeycloak, port: 8080, tag: "26.0")
    .WithArgs("--features=preview")
    // for more details regarding disable-trust-manager
    // see https://www.keycloak.org/server/outgoinghttp#_client_configuration_command
    // IMPORTANT: use this command ONLY in local development environment!
    .WithArgs("--spi-connections-http-client-default-disable-trust-manager=true")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent)
    .RunKeycloakWithHttpsDevCertificate(port: 8081);

var idp = builder.AddProject<Projects.IdentityExternalErrorHandling>("idp")
    .WithExternalHttpEndpoints()
    .WithReference(keycloak)
    .WaitFor(keycloak);

builder.Build().Run();