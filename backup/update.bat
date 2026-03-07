echo Update all Files...

echo Backup scripts
copy *.pl /y "C:\Users\Victor Salvador\Documents\GitHub\mis-archivos\backup"
copy *.bat /y "C:\Users\Victor Salvador\Documents\GitHub\mis-archivos\backup"

call cd ..\PERSONAL
echo Upload files
copy personal.m3u "C:\Users\Victor Salvador\Documents\GitHub\mis-archivos"
copy extra.m3u "C:\Users\Victor Salvador\Documents\GitHub\mis-archivos"
copy gatotv.xml "C:\Users\Victor Salvador\Documents\GitHub\mis-archivos"
copy mitv.xml "C:\Users\Victor Salvador\Documents\GitHub\mis-archivos"
copy epgshare*.xml "C:\Users\Victor Salvador\Documents\GitHub\mis-archivos"
copy "c:\tmp\*.tmb" "C:\Users\Victor Salvador\Documents\GitHub\mis-archivos\bk.tmb"

echo Backup scripts
copy *.pl /y "C:\Users\Victor Salvador\Documents\GitHub\mis-archivos\backup"
copy *.bat /y "C:\Users\Victor Salvador\Documents\GitHub\mis-archivos\backup"

C:
cd "C:\Users\Victor Salvador\Documents\GitHub\mis-archivos"

git add -A
git commit -m "Actualizo todo"
git push --force origin main