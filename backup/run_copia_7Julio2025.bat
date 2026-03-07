echo Clean olds...
call del ..\PERSONAL\mitv*.xml
call del ..\PERSONAL\epgshare*.xml
call del ..\PERSONAL\gatotv.xml
call del ..\PERSONAL\new_spin.m3u

echo Running gatotv.com...
call npm run grab -- --days 3 --maxConnections 15 --site=gatotv.com --output=../PERSONAL/gatotv.xml

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

echo Download new files M3U
call download_spin.pl

echo Refresh channels S1
call refresh_spin.pl personal.m3u new_spin.m3u S1

echo Refresh channels S2
call refresh_spin.pl personal.m3u new_spin.m3u S2

echo Refresh channels S3
call refresh_spin.pl personal.m3u new_spin.m3u S3

echo Refresh channels S4
call refresh_spin.pl personal.m3u new_spin.m3u S4


::echo Create new guide.xml
::echo call make_guide.pl personal.m3u

echo Download EPG Share
call download_epgshare.pl

::echo Make EPG Share XML
::epg_colapsar.pl new_epgshare.xml epgshare.xml

echo Upload files
copy personal*.m3u "c:\Users\pelon\OneDrive - valmersys.com\Documentos\GitHub\mis-archivos"
copy extra*.m3u "c:\Users\pelon\OneDrive - valmersys.com\Documentos\GitHub\mis-archivos"
copy gatotv*.xml "c:\Users\pelon\OneDrive - valmersys.com\Documentos\GitHub\mis-archivos"
copy mitv*.xml "c:\Users\pelon\OneDrive - valmersys.com\Documentos\GitHub\mis-archivos"
copy epgshare*.xml "c:\Users\pelon\OneDrive - valmersys.com\Documentos\GitHub\mis-archivos"



cd "c:\Users\pelon\OneDrive - valmersys.com\Documentos\GitHub\mis-archivos"

git add -A
git commit -m "Actualizo todo"
git push --force origin main