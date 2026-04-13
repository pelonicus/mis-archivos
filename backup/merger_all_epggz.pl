#!/usr/bin/perl
use strict;
use warnings;
use IO::Uncompress::Gunzip qw(gunzip $GunzipError);
use IO::Compress::Gzip qw(gzip $GzipError);

my %channels;
my %programmes;

my @files = glob("*.gz");

foreach my $file (@files) {

    print "Procesando $file...\n";

    my $gz = IO::Uncompress::Gunzip->new($file)
        or die "No puedo abrir $file: $GunzipError\n";

    while (my $line = <$gz>) {

        $line =~ s/\r//g;
        $line =~ s/^\s+//;
        $line =~ s/\s+$//;
        next if $line eq "";

        # CHANNEL
        if ($line =~ /<channel[^>]*id="([^"]+)"/) {
            $channels{$1} = $line;
        }

        # PROGRAMME
        if ($line =~ /<programme[^>]*channel="([^"]+)"/) {

            # normalizar timezone
            $line =~ s/([+-]\d{4})/+0000/g;

            push @{ $programmes{$1} }, $line;
        }
    }

    close $gz;

    # borrar el gz ya procesado
    unlink $file or warn "No se pudo borrar $file\n";
}

open(my $out, ">", "epgshare.xml") or die "No puedo crear epgshare.xml\n";

print $out "<tv>\n";

foreach my $id (sort keys %channels) {

    my $count = exists $programmes{$id} ? scalar @{ $programmes{$id} } : 0;
    my $line  = $channels{$id};

    if ($line =~ /<display-name>/) {
        $line =~ s|<display-name>(.*?)</display-name>|<display-name>$1($count)</display-name>|;
    }
    else {

        if ($line =~ /<\/channel>/) {
            $line =~ s|</channel>|<display-name>$id($count)</display-name></channel>|;
        }
        else {
            $line .= "<display-name>$id($count)</display-name></channel>";
        }
    }

    print $out "$line\n";
}

foreach my $id (sort keys %programmes) {
    foreach my $p (@{ $programmes{$id} }) {
        print $out "$p\n";
    }
}

print $out "</tv>\n";

close($out);

print "XML generado\n";

# comprimir resultado final
gzip 'epgshare.xml' => 'epgshare.xml.gz'
    or die "Error comprimiendo: $GzipError\n";

unlink 'epgshare.xml';

print "Archivo final epgshare.xml.gz creado\n";