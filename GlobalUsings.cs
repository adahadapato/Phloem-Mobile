// Contracts are split into folders (Commands, Dtos, Enums, Queries), each with its own namespace.
// These make them available in every file, so no file needs its own using lines for them.
// This is a convenience for the developer, but it does mean that if you add a new contract, you need to add it here too.
global using Phloem.Services.Api.Contracts;
global using Phloem.Services.Api.Contracts.Commands;
global using Phloem.Services.Api.Contracts.Dtos;
global using Phloem.Services.Api.Contracts.Enums;
global using Phloem.Services.Api.Contracts.Queries;