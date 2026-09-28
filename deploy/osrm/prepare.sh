#!/bin/sh
# Preprocesses the OSM extract for the multi-level Dijkstra algorithm (once – the volume keeps the result).
# Takes under a minute for one voivodeship; the whole of Poland takes much longer and needs several GB of RAM.
set -eu

if [ -f /data/region.osrm.mldgr ]; then
    echo "OSRM data already prepared."
    exit 0
fi

osrm-extract -p /opt/car.lua /data/region.osm.pbf
osrm-partition /data/region.osrm
osrm-customize /data/region.osrm
