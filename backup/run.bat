echo Clean olds...
call del ..\PERSONAL\*.xml
call del ..\PERSONAL\*.gz*

echo Backup scripts
copy *.pl /y "C:\Users\Victor Salvador\Documents\GitHub\mis-archivos\backup"
copy *.bat /y "C:\Users\Victor Salvador\Documents\GitHub\mis-archivos\backup"


echo Running gatotv.com...
call npm run grab -- --days 7 --maxConnections 15 --site=gatotv.com --output=../PERSONAL/gatotv.xml

::echo Running make mi.tv XML...
::call make_mitv.pl
::echo Running mi.tv...
::call npm run grab -- --days 3 --channels=./custom_mitv.xml --output=..\PERSONAL\guide_2_mitv_all.xml

echo Running mi.tv MX...
call npm run grab --- --days 3 --maxConnections 15 --channels=sites/mi.tv/mi.tv_mx.channels.xml --output=../PERSONAL/mitv_MX.xml

echo Running mi.tv SV...
call npm run grab --- --days 3 --maxConnections 15 --channels=sites/mi.tv/mi.tv_sv.channels.xml --output=../PERSONAL/mitv_SV.xml

echo Running mi.tv PE...
call npm run grab --- --days 3 --maxConnections 15 --channels=sites/mi.tv/mi.tv_pe.channels.xml --output=../PERSONAL/mitv_PE.xml

echo Running mi.tv CO...
call npm run grab --- --days 3 --maxConnections 15 --channels=sites/mi.tv/mi.tv_co.channels.xml --output=../PERSONAL/mitv_CO.xml

echo Running mi.tv CL...
call npm run grab --- --days 3 --maxConnections 15 --channels=sites/mi.tv/mi.tv_cl.channels.xml --output=../PERSONAL/mitv_CL.xml

echo Running mi.tv AR...
call npm run grab --- --days 3 --maxConnections 15 --channels=sites/mi.tv/mi.tv_ar.channels.xml --output=../PERSONAL/mitv_AR.xml

call cd ..\PERSONAL

echo Conteo de programaciones GatoTV
call conteo_de_programaciones.pl gatotv.xml

echo All MiTV to mitv.xml
call mitv_all_to_file.pl

echo Download EPG Share
call download_epgs.pl

echo Procesar all epgs 
call process_all_epgs.pl

echo Split 50MB
call split_xml_50mb.pl all_epgshare.xml

echo Actualizar fecha
call actualizar_fecha.pl personal.m3u

echo Backup scripts
robocopy "D:\Users\VictorSalvador\source" "C:\Users\VictorSalvador\OneDrive - valmersys.com\Documentos\GitHub\mis-archivos\backup\VisualStudio2026\source" /MIR /NFL /NDL /NJH /NJS
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
