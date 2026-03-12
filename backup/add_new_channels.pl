#!/usr/bin/perl
use strict;
use warnings;

my $tag = shift @ARGV or die "Uso: add_new_channels.pl TAG\n";

my $personal = "personal.m3u";
my $nuevos   = "nuevos.m3u";

# -------- leer nuevos ----------
open(my $nf,"<",$nuevos) or die "No puedo abrir nuevos.m3u\n";
my @n = <$nf>;
close($nf);

my @canales_nuevos;

for(my $i=0;$i<@n;$i++){
    if($n[$i] =~ /^#EXTINF/){
        push @canales_nuevos, $n[$i].$n[$i+1];
    }
}

# -------- leer personal ----------
open(my $pf,"<",$personal) or die "No puedo abrir personal.m3u\n";
my @p = <$pf>;
close($pf);

my %visto;
my @orden;

for(my $i=0;$i<@p;$i++){

    next unless $p[$i] =~ /^#EXTINF.*?,(.*)/;

    my $nombre=$1;
    $nombre =~ s/^\s+//;

    my $base=$nombre;
    $base =~ s/\(.*?\)//g;
    $base =~ s/\s+$//;

    next if $visto{$base};

    $visto{$base}=1;
    push @orden,$base;
}

my %insertar;

# -------- preguntar ----------
foreach my $base (@orden){

    print "\nBuscando coincidencias para: $base\n";

    my @palabras = grep {length($_)>=3} split(/\s+/,lc $base);

    my %res;
    my $num=1;

    foreach my $c (@canales_nuevos){

        if($c =~ /,(.*)/){
            my $nom=$1;
            chomp $nom;

            foreach my $p (@palabras){

                if(lc($nom)=~/\Q$p\E/){
                    print "$num) $nom\n";
                    $res{$num}=$c;
                    $num++;
                    last;
                }
            }
        }
    }

    next if $num==1;

    print "Elegir canales (ej: 1,3) o Enter para ninguno: ";
    my $sel=<STDIN>;
    chomp $sel;
    next if $sel eq "";

    my @sel=split(/,/,$sel);

    foreach my $s(@sel){
        push @{ $insertar{$base} }, $res{$s} if exists $res{$s};
    }
}

# -------- insertar ----------
my @final;

for(my $i=0;$i<@p;$i++){

    if($p[$i] =~ /^(#EXTINF.*?,)(.*)/){

        my $extinf_prefix=$1;
        my $nombre=$2;

        $nombre =~ s/^\s+//;

        my $base=$nombre;
        $base =~ s/\(.*?\)//g;
        $base =~ s/\s+$//;

        if(exists $insertar{$base}){

            foreach my $nuevo (@{ $insertar{$base} }){

                my ($ext,$url)=split(/\n/,$nuevo);

                my $nuevo_extinf = $extinf_prefix.$base."($tag)\n";

                push @final,$nuevo_extinf;
                push @final,$url."\n";
            }

            delete $insertar{$base};
        }
    }

    push @final,$p[$i];
}

open(my $out,">",$personal) or die "No puedo escribir personal.m3u\n";
print $out @final;
close($out);

print "\nProceso terminado\n";