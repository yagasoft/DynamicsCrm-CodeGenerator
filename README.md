# DynamicsCrm-CodeGenerator

[![Join the chat at https://gitter.im/yagasoft/DynamicsCrm-CodeGenerator](https://badges.gitter.im/yagasoft/DynamicsCrm-CodeGenerator.svg)](https://gitter.im/yagasoft/DynamicsCrm-CodeGenerator?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

---

A Visual Studio extension for generating early bound classes for Microsoft Dynamics CRM entities based on a template file, similar to Entity Framework.

## Features

  + Built for Visual Studio
    + You never have to leave Visual Studio to regenerate the code
	+ All the configurations are saved in the project itself, which facilitates Version Control
  + Preserved the original CrmSvcUtil structure and logic
  + Customize the way the code is generated
    + You get a default T4 template for the code that is generated, with a multitude more features than the official tool (features below)
	+ You can rewrite the whole template if you wish for any possible requirements
  + Replaced the SDK types with .NET types
    + E.g. OptionSetValue => Enum (Enum[] for Multi-select), EntityReference => Guid, Money => decimal, ... etc.
  + Generate only what's needed
    + Only choose the entities required
    + Only the fields required
  + Additional control
    + Option to use display names of entities and fields as variable names instead of logical names
    + Override field names inside the tool's UI
    + Ability to Lock variable names to avoid code errors on regeneration
  + Greatly enhanced regeneration speed by only fetching changed metadata from the server
  + Support for strongly-typed alternate keys, for entities and Entity References
  + Add annotations for model validation
  + Generate metadata
    + Field logical and schema names
    + Localised labels
  + Automatically limit attributes retrieved from CRM on any entity in a LINQ to the ones choosen (filtered) in the tool (check new entity constructors)
  + Many options to optimise generated code size even further
  + Define web service contracts with different profiles
    + Option to mark certain fields as 'read-only'
    + Option to link CRM entity profile with contract profiles, which effectively copies selection changes made in contracts to the CRM entity
  + Generate concrete classes for CRM Actions
  + Support bulk relation loading
    + Support filtering on relation loading

This tool is available as an XrmToolBox plugin as well ([here](https://www.xrmtoolbox.com/plugins/plugininfo/?id=45abdb43-f0e5-ea11-bf21-281878877ebf)).

## How To Use

You can read a quick overview of the tool and its functionality [here](https://blog.yagasoft.com/2020/09/dynamics-template-based-code-generator-supercharged).

To get started, install the Visual Studio extension ([here](https://marketplace.visualstudio.com/items?itemName=Yagasoft.CrmCodeGenerator)).

Note: any window can be closed by pressing the _ESC_ button on the keyboard, even if the generator is busy.

#### Add a template to your project

Highlight the project where you want to add the template and generated code.   
Click on Tools –> Add CRM Code Generator Template... (if you don't see this menu, then shutdown VS and reinstall the extension).

![File](https://github.com/yagasoft/DynamicsCrm-CodeGenerator/raw/master/guide/images/crm-generator-external-06.png)

  + Start with one of the provided templates.
  + After a template is added to your project you will be prompted for CRM connection info.
  + Pick the entities that you want to include.
  + Click the "Generate Code" button.

If you make schema changes in CRM and you want to refresh the code, right click the template and select "Run Custom Tool".

![File](https://github.com/yagasoft/DynamicsCrm-CodeGenerator/raw/master/guide/images/crm-generator-external-07.png)

#### Changing the template

When you make changes to the template and save, Visual Studio will automatically attempt to regenerate the code.

## Screenshots

![File](https://github.com/yagasoft/DynamicsCrm-CodeGenerator/raw/master/guide/images/crm-generator-external-01.png)

![File](https://github.com/yagasoft/DynamicsCrm-CodeGenerator/raw/master/guide/images/crm-generator-external-02.png)

![File](https://github.com/yagasoft/DynamicsCrm-CodeGenerator/raw/master/guide/images/crm-generator-external-03.png)

![File](https://github.com/yagasoft/DynamicsCrm-CodeGenerator/raw/master/guide/images/crm-generator-external-04.png)

![File](https://github.com/yagasoft/DynamicsCrm-CodeGenerator/raw/master/guide/images/crm-generator-external-05.png)

## Upcoming/planned

+ Add: Visual Studio Shared Projects support
+ Add: undo/redo
+ Add: parse placeholders in annotations for LogicalName and variable name
+ Add: [template] File field, with a Get and Set that uses the SDK message
+ Add: enum annotations (DisplayName ... etc.)
+ Add: option to add alternate keys to contracts
+ Add: option to use CRM-only contracts to replace Contract classes, optionally
+ Add: [template] an attribute/annotation to the Clear Flag fields in contracts, to ease parsing them in helper methods
+ Add: [template] XrmDefinitelyTyped support, and generate form structure
+ Improve: [template] rework lookup labels localisation
  + After the upgrade to v7, it has been buggy, and I don't like how it uses ExecuteMultiple to load labels in the first place; so I need to come up with a method that is faster but still as efficient
+ Fix: [template] helpers to support new stuff since v7
  + LoadRelation methods

## Credits & Contributions

+ [Eric Labashosky](https://github.com/xairrick):
  + Original extension base code
  + My work:
    + Redesigned the screens and most of the logic
    + Greatly boosted the generation and regeneration speed
    + Added the features that don't exist in the Microsoft's tool
+ Ramy Victor:
  + Enhancements
  + Debugging during the early versions
+ Mohammed Ghoname:
  + New feature suggestions during the early versions
+ [ClemensWon](https://github.com/ClemensWon):
  + Global option-sets

## Changes
+ Check Releases page for the later changes
#### _v10.6 to v10.9.1 (since 2020-10-01)_
+ Added: CRM entity profiles clean-up
+ Added: Filter Details window row filtering
+ Added: [template] unify global option-sets (thanks https://github.com/ClemensWon)
+ Added: cell highlighter for easier interaction with grids
+ Improved: sort profiles to aid in preventing source control merge conflicts in JSON
+ Improved: JSON size by removing even more redundant information
+ Improved: connection readiness performance
+ Improved: upgraded to VS Async API
+ Improved: refactor
+ Fixed: Actions 'loading' indicator issue
+ Fixed: generated code 'labels' syntax error
+ Fixed: connection errors causing deadlocks
+ Fixed: connection issues
+ Fixed: [template] removed LimitAttributes, which caused issues in sandbox plugins
+ Fixed: [template] skip Guid.Empty-valued records when calling ProcessFetchXmlRelations
+ Fixed: changing contract settings that doesn't require a trip to CRM's servers is not reflected if cache-only is used
+ Fixed: FetchXML result processing helper
+ Fixed: pre-v7 migration
+ Fixed: rare 'object null reference' error during generate
+ Fixed: filter to properly take the Display Name and Rename columns into account as lower case, and to consider the 'entity' and 'field' columns in relations
+ Fixed: concurrency issues
+ Fixed: Use Display Names option not saved correctly
+ Fixed: non-existent entities in settings persisting, causing error with 'generate cached'
+ Fixed: cache not working
+ Fixed: [template] ActivityParty naming issue (thanks https://github.com/ClemensWon)
+ Fixed: [template] consider the 'use display names' setting for global option-sets
+ Fixed: [template] multiselect option-sets mapping to global option-sets (thanks https://github.com/ClemensWon)
#### _v10.1 to v10.5 (since 2020-08-28)_
+ Added: option to link CRM entity profile with contract profiles, effectively copying selection changes made in contracts to the CRM entity to keep them in sync
+ Added: option to define entity, attribute, and contract custom annotations through the UI
+ Added: [template] helper to automatically limit attributes retrieved from CRM on any entity in a LINQ to the ones choosen (filtered) in the tool (check new entity constructors)
+ Added: [template] helper to automatically parse Aliased Values into their respective early-bound properties in entity records retrieved by a FetchXML query
+ Added: option to split contracts (profile groups) into separate files, to ease sharing with other teams
+ Added: template file compatibility check (validates using version formatted as in default templates)
+ Added: CRM Entity Profile window in the Entity Selection (double-click CRM Entity to open)
+ Added: filtering option in the Entity Selection (if checked, Profile is applied, else only renaming is applied)
+ Added: option to automatically delete unselected Profiles on save
+ Added: button to clear cached data from memory without having to generate or refresh
+ Added: 'cancel' option in the Entity Selection window
+ Added: connections warm-up to improve code generation performance
+ Added: pre-v10 settings migration
+ Improved: tool startup speed
+ Improved: mapper error-handling
+ Improved: handling the extremely rare 'null reference' error by showing a meaningful message, which indicates that clearing the cache fixes the issue (still trying to catch it)
+ Improved: [template] small enhancements
+ Improved: contracts are now grouped into a single separate file by default (even if the option is disabled), with a base file for common code with CRM
+ Improved: base and contract files do not require the SDK assemblies anymore, to ease sharing with non-CRM teams
+ Improved: [template] removed annotation attribute references when none need to be generated to save an assembly reference
+ Improved: table rendering performance
+ Improved: reduced connection pool timeout (20 secs) to get a new connection faster when none are available
+ Improved: cache folder structure
+ Improved: cache performance
+ Improved: log messages
+ Changed: the 'red' row indicator in the Entity Selection window to mean that the CRM Entity Profile has some data
+ Changed: [template] fell back to an older version of C# to avoid tranformation errors on older VS versions
+ Fixed: connection string values containing '=' character causing connectivity issue (e.g. client secrets containing '=')
+ Fixed: 'missing assembly' errors (if one still persists, please report it)
+ Fixed: cancelling the mapper (ESC button) causes the window to rarely hang
+ Fixed: [template] ConvertTo and GetLabel issues
+ Fixed: stop warmup on changing connection
+ Fixed: duplicate profiles saved in JSON
+ Fixed: [template] ApplyFilter issues
+ Fixed: [template] data annotations issue
+ Fixed: [template] helper method issues
+ Fixed: [template] issues
+ Fixed: Actions selection issue
+ Fixed: settings reset issue
+ Fixed: filtering issue
+ Fixed: deadlock issue
+ Fixed: issue with Enable rules in Options window
+ Fixed: alternate key information retrieved regardless of setting
+ Fixed: Generate Global Actions setting having no effect
+ Fixed: issue with pre-v9 settings migration
+ Fixed: issues
+ Removed: 'Apply to CRM Entity' option and moved it to Entity Selection window
+ Removed: metadata refresh buttons
+ Removed: redundant and obsolete settings entries
#### _v7 to v9 (since 2020-08-09)_
+ Added: Multi-option Option-set and Image support (File Field requires a special SDK Message to handle)
+ Added: new Annotation Attributes for Image Fields
+ Added: Entity list view filtering in profiles
+ Added: selection control for CRM Actions
+ Added: [template] custom multi-typed EntityReference type (Customer, Owner ... etc.)
+ Added: options to optimise the resulting code by removing some extra data
+ Added: option to optimise settings file size
+ Added: 'cancel' option in the Profiles window
+ Added: pre-v9 settings migration
+ Improved: [template] use .NET Annotation Attributes when possible, instead of the custom ones
+ Improved: optmised CRM Actions generation
+ Improved: increased threading of independent logic
+ Improved: optimised saved and generated data
+ Improved: switched to EnhancedOrgService package for improved connection pooling and caching
+ Improved: moved the 'Apply to CRM Entity' option to be on a per-Entity level for more control
+ Improved: 'Apply to CRM Entity' filters Attributes and Relations similar to how Contracts work
+ Improved: [template] label dictionary property initialisation
+ Improved: cleaned and refactored to prepare for XrmToolBox Plugin and improve extensibility
+ Improved: error messages
+ Changed: switched to explicit or raw Connection Strings to allow for a broader support of newer features
+ Changed: separated settings from cache data (cache saved at the solution level)
+ Changed: save settings as JSON (per project)
+ Changed: save connection in a separate file outside of source control
+ Changed: exclude cache data from Source Control
+ Updated: SDK packages
+ Updated: licence
+ Fixed: issue with assembly binding
+ Fixed: handling older versions of CRM when it comes to new features
+ Fixed: [template] contract label dictionary has wrong type definition
+ Fixed: issues
+ Removed: connection profiles to encourage generating from a single model source for the selected project
#### _v1.1.1 (2015-03-01)_
+ Initial release

---
**Copyright &copy; by Ahmed Elsawalhy ([Yagasoft](https://yagasoft.com))** -- _GPL v3 Licence_
