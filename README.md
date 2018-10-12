# patient-education-assembler
Automated download and transformation of existing patient education resources into RTF + Access metadata for Cerner Patient Education

Warning: This software is incomplete.  It is provided now so that you can review the design and start work on analysing web based resources that you wish to import.

To install this software:
- Download Visual Studio, install including the option to develop addons for Microsoft Office
- Open the solution in VS
- Download required packages with NuGet (Project > Manage nuget packages)
- You may need to point the software at your particular version of the Office:
  - Right click Resources under the Patient Education Assembler project and select Add Reference
  - Select COM
  - Find and select Microsoft Word xxxx Object Library

To use the software:
Run the project
Accept the warning about ensuring you have permission
Under the General tab, Click "Select..." to the right of output directory
Under the Content Provider tab, click Select and load the content provider specification file
Click Load One Document to download the index and download/parse the first document.