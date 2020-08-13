# DynamicsCrm-CodeGenerator

[![Join the chat at https://gitter.im/yagasoft/DynamicsCrm-CodeGenerator](https://badges.gitter.im/yagasoft/DynamicsCrm-CodeGenerator.svg)](https://gitter.im/yagasoft/DynamicsCrm-CodeGenerator?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

### Version: 8.1.1
---

A Visual Studio extension that allows generating early bound classes for Microsoft Dynamics CRM entities based on a template file, similar to Entity Framework.

### Features

  + Preserved the original CrmSvcUtil structure and logic
	+ Replaced the SDK types with .NET types
	  + E.g. OptionSetValue => Enum (Enum[] for Multi-select), EntityReference => Guid, Money => decimal, ... etc.
	+ Only choose the Entities required
	+ Option to use Display Names of entities and fields as Variable names instead of Logical Names
	+ Option to override Field names inside the tool's UI
	+ Option to lock Variable names to avoid code errors on regeneration
  + Greatly enhanced regeneration speed by only fetching changed metadata from the server
  + Support for strongly-typed Alternate Keys, for Entities and Entity References
  + Generate Metadata
	+ Field Logical and Schema Names
	+ Localised labels
  + Many options to optimise generated code size
  + Define web service contracts
	+ Filter attributes to reduce size
	+ Add Annotations for model validation
	+ Option to mark certain Fields as 'ready only'
  + Generate concrete classes for CRM Actions
  + Support bulk relation loading
	+ Support filtering on relation loading

### Benefits of using this tool over the standard tool

  + Control which entities to generate classes.
	+ Optmises the size of the generated code, keeping it at the minimum required.
    + If you use the CrmSvcUtil.exe to generate, the code file will be 200,000 lines, compared to ~1000 lines for each entity you select.
  + Customize the way the code is generated
    + You get a default T4 template for the code that is generated, with a multitude more features than the official tool.
	+ The template is fully customisable (even from scratch) for any possible needs that arise.
  + Built for Visual Studio
    + You never have to leave Visual Studio to regenerate the code.
	+ All the configurations are saved in the project itself, which facilitates Version Control.

## How To Use

I will post a complete guide soon ...

Install the Visual Studio extension.

#### Add a template to your project

Highlight the project where you want to add the template and generated code.   
Click on Tools –> Add CRM Code Generator Template... (if you don't see this menu, then shutdown VS and reinstall the extension).

![File](http://blog.yagasoft.com/wp-content/uploads/2020/08/crm-generator-external-06.png)

  + Start with one of the provided templates.
  + After a template is added to your project you will be prompted for CRM connection info.
  + Pick the entities that you want to include.
  + Click the "Generate Code" button.

If you make schema changes in CRM and you want to refresh the code, right click the template and select "Run Custom Tool".

![File](http://blog.yagasoft.com/wp-content/uploads/2020/08/crm-generator-external-07.png)

#### Changing the template

When you make changes to the template and save, Visual Studio will automatically attempt to regenerate the code.

### Screenshots

![File](http://blog.yagasoft.com/wp-content/uploads/2020/08/crm-generator-external-01.png)

![File](http://blog.yagasoft.com/wp-content/uploads/2020/08/crm-generator-external-02.png)

![File](http://blog.yagasoft.com/wp-content/uploads/2020/08/crm-generator-external-03.png)

![File](http://blog.yagasoft.com/wp-content/uploads/2020/08/crm-generator-external-04.png)

![File](http://blog.yagasoft.com/wp-content/uploads/2020/08/crm-generator-external-05.png)

### Credits

  + Base code:
	+ Eric Labashosky
	+ https://github.com/xairrick/CrmCodeGenerator
  + My work:
	+ Completely reworked the screens
	+ Greatly enhanced the generation and regeneration speed
	+ Added the features that don't exist in the official tool

## Changes

#### _v8.1.1 (2020-08-14)_
+ Added: Multi-option Option-set and Image support (File Field requires a special SDK Message to handle)
+ Added: new Annotation Attributes for Image Fields
+ Added: Custom multi-typed EntityReference type (Customer, Owner ... etc.)
+ Added: options to optimise the resulting code by removing some extra data
+ Added: selection control for CRM Actions
+ Improved: use .NET Annotation Attributes when possible, instead of the custom ones
+ Improved: label dictionary property initialisation
+ Improved: optmised CRM Actions generation
+ Improved: increased threading of independent logic
+ Improved: optimised saved and generated data
+ Improved: cleaned and refactored code
+ Fixed: contract label dictionary wrong type definition
+ Fixed: issues
#### _v7.1.3 (2020-08-11)_
+ Changed: save connection in a separate file outside of source control
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

---
**Copyright &copy; by Ahmed Elsawalhy ([Yagasoft](http://yagasoft.com))** -- _GPL v3 Licence_
