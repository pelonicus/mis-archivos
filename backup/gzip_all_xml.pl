#!/usr/bin/perl
use strict;
use warnings;
use IO::Compress::Gzip qw(gzip $GzipError);

# -------- LISTADO DE ARCHIVOS A COMPRIMIR --------
my @xml_files = (
    "gatotv.xml",
    "mitv.xml",
	"epgshare.xml",

);

print "Iniciando compresion...\n\n";

foreach my $xml (@xml_files) {

    unless (-e $xml) {
        print "No existe: $xml\n";
        next;
    }

    my $gz = "$xml.gz";

    print "Comprimiendo $xml -> $gz\n";

    gzip $xml => $gz
        or do {
            print "Error comprimiendo $xml: $GzipError\n";
            next;
        };

    unlink $xml or warn "No se pudo borrar $xml\n";
}

print "\n====================================\n";
print "Proceso terminado.\n";