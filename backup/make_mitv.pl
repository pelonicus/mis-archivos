#!C:\strawberry\perl\bin\perl.exe;

open $m3u_handle, '<', "../PERSONAL/personal.m3u";
	chomp(@m3u_lines = <$m3u_handle>);
close $m3u_handle;

@files = glob("mi\.tv_*\.xml");
foreach my $archivo (@files) {
    open(my $fh, '<', $archivo) or die "No se pudo abrir el archivo '$archivo': $!";
		push @all_xml, <$fh>;
    close($fh);
}

push @new_mitv , "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n";
push @new_mitv , "<channels>\n";

foreach $each_m3u (@m3u_lines) {
	@m3u_kv = split /\"/, $each_m3u, 3;
#print "$m3u_kv[1]\n";
	if($m3u_kv[1] eq ""){ next; }
	if($last_m3u eq $m3u_kv[1]){ next; };
	foreach $each_all_xml (@all_xml) {
		@xml_kv = split /\"/, $each_all_xml;
#print "$xml_kv[7] eq $m3u_kv[1]\n";
		if($xml_kv[7] eq $m3u_kv[1] || $xml_kv[5] eq $m3u_kv[1]) {
#print "$m3u_kv[1] : $each_all_xml\n";
			push @new_mitv , $each_all_xml;
			$last_m3u=$m3u_kv[1];
			last;
		}

	}
}
push @new_mitv , "</channels>";

open (FILEHANDLE, ">custom_mitv.xml") or die ("Cannot open custom_mitv.xml");
	print FILEHANDLE join("",@new_mitv);
close FILEHANDLE;