# Discordbot Project Setup

> **Note:** This is a historical document capturing the initial project setup notes. All items documented here have been completed. For current development status, see [ROADMAP.md](../../ROADMAP.md).

I'm going to be creating a discord bot using a similar design as a pervious never-finished version I built. But first let's draft the project setup. 


## Setup Documentation Folder

- Create a docs/ folder
- Have the system architect create a small draft doc of a requirements:
  - .NET 8
  - Hosted Service for bot worker
  - .NET Web API for web management interface
  - SQL Database: SQLite for dev/test, MSSQL, MySQL or Postgres for production
  - Serilog for log provider, Microsoft logging framework ILoggers injected via DI
  - Client UI: Razor pages calling web API backend
  - Custom design system: to be designed by designer with a proposed color scheme.
  - Front end can use hero icons and tailwindcss if appropriate
  - Discord.NET bot framework - Register as a singleton in DI, our bot service will manage the framework bot client and handle registering event handlers, commands, etc. 

## Design & Initial Web Prototype

- Have the designer use the follow color scheme to draft a client web UI design and style guideline:
  - ```  
    #1d2022 - Background
    #d7d3d0 - Primary
    #cb4e1b - Accent 1
    #098ecf - Accent 2
    ```
- Have the prototyper use the design guide to draft a sample main layout and overview dashboard for a admin/bot management razor pages.

## .NET Project Setup

- Create a src/ folder and create appropriate projects for:
  - Business Layer projects/class library - entities and services, etc. Core domain
  - Bot project - .NET Web API with razor pages support, and a background service running the discord bot
  - Tests - Project for unit tests

## System Planning

- Create a high-level plan for implementation of an MVP:
  - Full implementation of discord.net framework bot
  - Basic /ping and admin commands
  - Command framework: System for detection and registration and command handling delegation, using slash commands, and supporting interactivity. 
  - Data entities, DTOs, basic API controllers
  - Proper logging throughout core services

## Github Setup

- Create github issues for the plan
  - Create appropriate labels
  - Create top level Epics
  - Create Features for the Epics
  - Plan tasks for features. **Important**: plan only, describe it breifly as sub items on the Feature, and we'll write proper scope for the tasks later. 

