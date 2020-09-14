# patient-education-assembler
Automated download and transformation of existing patient education resources into RTF + Access metadata for Cerner Patient Education

Warning: This software is improving but is not yet feature complete

To install this software:
- Download Visual Studio, install including the option to develop addons for Microsoft Office
- Open the solution in VS
- Download required packages with NuGet (Project > Manage nuget packages)
- You may need to point the software at your particular version of the Office:
  - Right click Resources under the Patient Education Assembler project and select Add Reference
  - Select COM
  - Find and select Microsoft Word xxxx Object Library

To use the software:
Create a folder for output files
Copy the blank MS Access database into your output directory
Run the project
Accept the warning about ensuring you have permission (you should also ensure that you do :)
Under the General tab, Click "Select..." to the right of output directory to nominate the output directory you have created above
Also select the folder that contains your education material specifications
Under the Content Provider tab, click Select and load the content provider specification file
You can change the current content provider with the arrow buttons
In the Content Provider tab there is an option to Load Index.  This only downloads the index, not the individual education items.
After loading the index it is a good idea to click Finish to save the items to the database.
If you have previously run this tool, you will be presented with a dialog to reconcile any changes.  For example, your provider may have re-named items and thus changed their URLs; they may have removed items and added new items.
The software handles all of these scenarios.  For a change of URL, select the previous item on the left, and the replacement item on the right, and select Replace.  For new items select them and click Include New.  For left over items, click the Remove All Remaining Missing Documents button.
If you want to check loading and rendering of one document, you can also click Load One Document
To process all documents for a provider click Start This
To process all documents for all providers click Start All
To save information to the access database, ensure you click Finish


Planned Features
* Seamless opening of both rendered document and rendered source HTML
* Avoid opening, parsing and saving a document whose cached file is earlier than the rendered document, and no re-parse has been forced / triggered
