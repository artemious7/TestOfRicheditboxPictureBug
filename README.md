# TestOfRicheditboxPictureBug
In Windows 10 1703 Creators Update a new bug appeared.

Steps to reproduce:
1. Load an RTF document with a picture into RichEditBox
2. Save it.
3. Save it again.
4. Reload it or open it with another app - you will see that the picture is gone and all the content after it is gone too.

This project is to demonstrate the bug and to fix it.

After you save the document for the 2-nd time, the picture is gone, but the closing brace '}' for the picture block is still there, so all the content that comes after the brace '}' cannot be read.

So we simply remove the odd brace and restore the picture that was gone using the same document (but unchanged one).
