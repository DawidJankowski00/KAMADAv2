#!/bin/sh
# Preprocesses the merged OSM extract for the multi-level Dijkstra algorithm (once per region list – the volume
# keeps the result). The four default voivodeships take a few minutes and a few GB of RAM.
set -eu

if [ -f /data/region.osrm.mldgr ] && [ "$(cat /data/region.regions)" = "$(cat /data/region.osrm.regions 2>/dev/null)" ]; then
    echo "OSRM data for '$(cat /data/region.regions)' already prepared."
    exit 0
fi

rm -f /data/region.osrm*
osrm-extract -p /opt/car.lua /data/region.osm.pbf
osrm-partition /data/region.osrm
osrm-customize /data/region.osrm
cp /data/region.regions /data/region.osrm.regions
