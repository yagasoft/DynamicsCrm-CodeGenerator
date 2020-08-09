# DynamicsCrm-CodeGenerator

[![Join the chat at https://gitter.im/yagasoft/DynamicsCrm-CodeGenerator](https://badges.gitter.im/yagasoft/DynamicsCrm-CodeGenerator.svg)](https://gitter.im/yagasoft/DynamicsCrm-CodeGenerator?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

### Version: 7.1.2
---

A Visual Studio extension that allows generating early bound classes for Microsoft Dynamics CRM entities based on a template file, similar to Entity Framework.

### Features

  + Metadata
  + Web service contracts
	+ Supports exclusion of saving certain values
  + CRM Actions concrete classes
  + Filtering of attributes to reduce size
  + Bulk relation loading
  + Filtering on relation loading
  + Use display names of entities and fields as variable names
  + Locking names to avoid code errors on regeneration

### Benefits of using this tool over the standard tool

  + Control which entities to generate classes for this will keep the size of the generated code to a minimum.
    + If you use the CrmSvcUtil.exe to generate, the code file will be 200,000 lines, compared to ~1000 lines for each entity you select.
  + Customize the way the code is generated
    + You get a default T4 template for the code that is generated, which gives full control over how the code is generated.
  + Built for Visual Studio
    + You never have to leave Visual Studio to regenerate the classes.
	+ All the configurations* are stored in the project which allows you save them to Source Control.

## How To Use
I will post a complete guide soon ...

Install the VS extension.

#### Add a template to your project
Highlight the project where you want to store the template and generated code.   
Tools –> Add CRM Code Generator Template... (if you don't see this menu, then shutdown VS and reinstall the extension)

![File](Documentation/image_thumb_2.png)

  + Start with one of the provided templates.
  + After a template is added to your project you will be prompted for CRM connection info.
  + Pick the entities that you want to include.
  + Click the "Generate Code" button.

If you make schema changes in CRM and you want to refresh the code, right click the template and select "Run Custom Tool".

![File](Documentation/image_thumb_1.png)

#### Changing the template
When you make changes to the template and save, Visual Studio will automatically attempt to re-generate the code.

### Credits

  + Base code:
	+ Eric Labashosky
	+ https://github.com/xairrick/CrmCodeGenerator
	+ My work:
		+ Reworked the screens
		+ Added caching
		+ Added a lot of new features

## Changes

#### _v7.1.2 (2020-08-10)_
+ Improved: connection pooling
+ Fixed: issues
#### _v7.1.1 (2020-08-09)_
+ Added: entity list filtering in profiles
+ Added: option to optimise settings size
+ Changed: switched to explicit Connection Strings to allow for a broader support of newer features
+ Changed: separated settings from cache data (cache saved at the solution level)
+ Changed: save settings as JSON (per project)
+ Changed: exclude cache data from Source Control
+ Updated: SDK packages
+ Improved: switched to EnhancedOrgService package for improved connection pooling and caching
+ Improved: performance
+ Fixed: issues
+ Removed: connection profiles to encourage generating from a single model source for the selected project
#### _v6.15.9 (2018-12-18)_
+ Fixed: thread deadlock
#### _v6.15.5 (2018-09-23)_
+ Fixed: connection string not picking up the password from the UI
#### _v6.15.4 (2018-09-13)_
+ Fixed: show missing messages in some entities
+ Fixed: image attribute list empty on first access to dialogue
+ Fixed: updating image throws exception

---
**Copyright &copy; by Ahmed Elsawalhy ([Yagasoft](http://yagasoft.com))** -- _GPL v3 Licence_
