use strict;
use warnings;
use File::Glob ':glob';

my @files = bsd_glob("mitv_*.xml");

my %channels;
my %programmes;
my %programme_cnt;
my %channel_order;
my @order;

foreach my $file (@files) {

    next if $file eq "mitv.xml";

    # Extraer país del nombre del archivo
    my $country = "";
    if ($file =~ /^mitv_([A-Za-z]+)\.xml$/) {
        $country = $1;
    }

    open(my $in, "<", $file) or die "No puedo abrir $file: $!";
    local $/ = undef;
    my $content = <$in>;
    close($in);

    while ($content =~ m{(<channel\b.*?</channel>|<programme\b.*?</programme>)}gs) {

        my $block = $1;

        if ($block =~ /^<channel\b/) {

            if ($block =~ m{id="([^"]+)"} ) {
                my $id = $1;

                unless (exists $channels{$id}) {
                    push @order, $id;
                    $channels{$id} = $block;
                    $channel_order{$id} = $country;   # guardar país
                }
            }
        }

        elsif ($block =~ /^<programme\b/) {

            if ($block =~ m{channel="([^"]+)"} ) {
                my $id = $1;
                push @{ $programmes{$id} }, $block;
                $programme_cnt{$id}++;
            }
        }
    }
}

open(my $out, ">", "mitv.xml") or die "No puedo crear mitv.xml: $!";

print $out qq{<?xml version="1.0" encoding="UTF-8"?>\n};
print $out qq{<tv generator-info-name="UnificadorEPG">\n};

# Imprimir canales
foreach my $id (@order) {

    my $block = $channels{$id};
    my $count = $programme_cnt{$id} // 0;
    my $country = $channel_order{$id} // "";

    # Formato: Nombre(200)(PE)
    $block =~ s{(<display-name[^>]*>)(.*?)(</display-name>)}{
        $1 . $2 . "($count)($country)" . $3
    }se;

    print $out "$block\n";
}

# Imprimir programmes agrupados
foreach my $id (@order) {

    if (exists $programmes{$id}) {
        foreach my $prog (@{ $programmes{$id} }) {
            print $out "$prog\n";
        }
    }
}

print $out "</tv>\n";
close($out);

print "mitv.xml generado con país y conteo correctamente.\n";