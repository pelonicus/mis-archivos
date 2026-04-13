echo Clean olds...
call del ..\PERSONAL\guide*.*

echo Running gatotv.com...
call npm run grab -- --days 3 --maxConnections 15 --site=gatotv.com --output=..\PERSONAL\guide_1_gatotv.xml

echo Running mi.tv SV...
call npm run grab -- --days 3 --maxConnections 15 --channels=.\sites\mi.tv\mi.tv_sv.channels.xml --output=.\guide_2_mitv_sv.xml

echo Running mi.tv PE...
call npm run grab -- --days 3 --maxConnections 15 --channels=.\sites\mi.tv\mi.tv_pe.channels.xml --output=.\guide_3_mitv_pe.xml

echo Running mi.tv MX...
call npm run grab -- --days 3 --maxConnections 15 --channels=.\sites\mi.tv\mi.tv_mx.channels.xml --output=.\guide_4_mitv_mx.xml

echo Running mi.tv CO...
call npm run grab -- --days 3 --maxConnections 15 --channels=.\sites\mi.tv\mi.tv_co.channels.xml --output=.\guide_5_mitv_co.xml

echo Running mi.tv CL...
call npm run grab -- --days 3 --maxConnections 15 --channels=.\sites\mi.tv\mi.tv_cl.channels.xml --output=.\guide_6_mitv_cl.xml

echo Running mi.tv AR...
call npm run grab -- --days 3 --maxConnections 15 --channels=.\sites\mi.tv\mi.tv_ar.channels.xml --output=.\guide_7_mitv_ar.xml

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


echo Create new guide.xml and upload
call make_guide.pl personal.m3u