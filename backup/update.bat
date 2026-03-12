echo Update all Files...

echo Backup scripts
copy *.pl /y "C:\Users\VictorSalvador\OneDrive - valmersys.com\Documentos\GitHub\mis-archivos\backup"
copy *.bat /y "C:\Users\VictorSalvador\OneDrive - valmersys.com\Documentos\GitHub\mis-archivos\backup"

echo Upload files
copy personal.m3u /y "C:\Users\VictorSalvador\OneDrive - valmersys.com\Documentos\GitHub\mis-archivos"
copy extra.m3u /y "C:\Users\VictorSalvador\OneDrive - valmersys.com\Documentos\GitHub\mis-archivos"
copy gatotv.xml /y "C:\Users\VictorSalvador\OneDrive - valmersys.com\Documentos\GitHub\mis-archivos"
copy mitv.xml /y "C:\Users\VictorSalvador\OneDrive - valmersys.com\Documentos\GitHub\mis-archivos"
copy epgshare*.xml /y "C:\Users\VictorSalvador\OneDrive - valmersys.com\Documentos\GitHub\mis-archivos"

C:
cd "C:\Users\VictorSalvador\OneDrive - valmersys.com\Documentos\GitHub\mis-archivos"

git add -A
git commit -m "Actualizo todo"
git push --force origin main
